using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.Union.HintNameHelper;
using static Macaron.Union.StringHelper;
using static Macaron.Union.TypeDeclarationHelper;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using static Microsoft.CodeAnalysis.SpecialType;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;
using static Microsoft.CodeAnalysis.TypeKind;

namespace Macaron.Union;

internal static class UnionGenerationModelFactory
{
    #region Types
    private sealed record TaggedUnionInterfaceContext(
        INamedTypeSymbol TypeSymbol,
        Location Location,
        ImmutableArray<Location> TypeArgumentLocations
    );

    private sealed record TaggedUnionCaseAttributeContext(
        ITypeSymbol TypeSymbol,
        Location TypeLocation,
        string? ParamName,
        Location ParamNameLocation,
        bool IsParamNameValid,
        byte? Tag,
        Location TagLocation
    );
    #endregion

    #region Static Methods
    public static AnalysisResult Create(
        GeneratorAttributeSyntaxContext context,
        AttributeData taggedUnionAttribute,
        CancellationToken cancellationToken
    )
    {
        var structDeclarationSyntax = (StructDeclarationSyntax)context.TargetNode;
        var targetTypeSymbol = context.TargetSymbol as INamedTypeSymbol;

        if (targetTypeSymbol == null)
        {
            return AnalysisResult.Failure.Empty;
        }

        var taggedUnionInterfaces = GetTaggedUnionInterfaceContexts(
            targetTypeSymbol,
            context.SemanticModel.Compilation,
            cancellationToken
        );

        if (taggedUnionInterfaces.Length == 0)
        {
            return new AnalysisResult.Failure(
                new UnionAnalysisError.TargetTypeMustImplementTaggedUnionInterface(
                    Location: taggedUnionAttribute.ApplicationSyntaxReference
                        ?.GetSyntax(cancellationToken)
                        .GetLocation()
                        ?? structDeclarationSyntax.GetLocation(),
                    TargetTypeName: structDeclarationSyntax.Identifier.ToString()
                )
            );
        }

        if (taggedUnionInterfaces.Length > 1)
        {
            return new AnalysisResult.Failure(
                new UnionAnalysisError.TargetTypeCannotImplementMultipleTaggedUnionInterfaces(
                    Location: taggedUnionInterfaces[1].Location,
                    TargetTypeName: structDeclarationSyntax.Identifier.ToString()
                )
            );
        }

        var caseCandidates = CreateUnionCaseCandidates(taggedUnionInterfaces[0]);

        if (!TryGetCaseAttributeContexts(
            attributes: context
                .TargetSymbol
                .GetAttributes()
                .Where(x => x.AttributeClass?.ToDisplayString() == TaggedUnionMetadataNames.TaggedUnionCaseAttribute),
            cancellationToken,
            out var caseAttributes
        ))
        {
            return AnalysisResult.Failure.Empty;
        }

        var errorsBuilder = ImmutableArray.CreateBuilder<UnionAnalysisError>();
        var isValid = true;

        isValid &= ValidateTargetTypeDeclaration(
            structDeclarationSyntax,
            errorsBuilder
        );
        isValid &= ValidateTargetTypeMembers(
            structDeclarationSyntax,
            targetTypeSymbol,
            errorsBuilder,
            cancellationToken
        );
        isValid &= ValidateCaseAttributes(
            structDeclarationSyntax,
            caseCandidates,
            caseAttributes,
            errorsBuilder
        );

        caseCandidates = ApplyCaseAttributes(
            caseCandidates,
            attributeMap: CreateCaseAttributeMap(caseAttributes)
        );

        isValid &= ValidateCaseTags(
            structDeclarationSyntax,
            caseCandidates,
            errorsBuilder
        );
        isValid &= ValidateCaseTypes(
            structDeclarationSyntax,
            caseCandidates,
            errorsBuilder
        );

        if (!isValid)
        {
            return new AnalysisResult.Failure(errorsBuilder.ToImmutable());
        }

        var casesBuilder = ImmutableArray.CreateBuilder<UnionCaseGenerationModel>();

        foreach (var caseCandidate in caseCandidates)
        {
            var caseTypeSymbol = caseCandidate.TypeSymbol;
            var caseModel = new UnionCaseGenerationModel(
                IsReferenceType: caseTypeSymbol.IsReferenceType,
                IsUnmanagedType: caseTypeSymbol.IsUnmanagedType,
                IsInterfaceType: caseTypeSymbol.TypeKind == Interface,
                FullyQualifiedTypeName: GetCaseTypeName(caseTypeSymbol),
                ParamName: caseCandidate.ParamName,
                Tag: caseCandidate.Tag
            );

            casesBuilder.Add(caseModel);
        }

        var model = new UnionGenerationModel(
            Namespace: !targetTypeSymbol.ContainingNamespace.IsGlobalNamespace
                ? targetTypeSymbol.ContainingNamespace.ToDisplayString()
                : "",
            ContainingTypes: GetContainingTypes(targetTypeSymbol)
                .Select(GetPartialTypeDeclarationString)
                .ToImmutableArray(),
            TypeName: targetTypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
            Cases: casesBuilder.ToImmutable(),
            HintName: GetTypeHintName(targetTypeSymbol)
        );

        return new AnalysisResult.Success(model);

        #region Local Functions
        static ImmutableDictionary<ITypeSymbol, TaggedUnionCaseAttributeContext> CreateCaseAttributeMap(
            ImmutableArray<TaggedUnionCaseAttributeContext> attributes
        )
        {
            var taggedUnionCaseAttributeMapBuilder =
                ImmutableDictionary.CreateBuilder<ITypeSymbol, TaggedUnionCaseAttributeContext>(
                    SymbolEqualityComparer.Default
                );

            foreach (var attribute in attributes)
            {
                taggedUnionCaseAttributeMapBuilder[attribute.TypeSymbol] = attribute;
            }

            return taggedUnionCaseAttributeMapBuilder.ToImmutable();
        }

        static ImmutableArray<UnionCaseCandidateContext> ApplyCaseAttributes(
            ImmutableArray<UnionCaseCandidateContext> candidates,
            ImmutableDictionary<ITypeSymbol, TaggedUnionCaseAttributeContext> attributeMap
        )
        {
            var builder = ImmutableArray.CreateBuilder<UnionCaseCandidateContext>(candidates.Length);

            foreach (var candidate in candidates)
            {
                if (attributeMap.TryGetValue(candidate.TypeSymbol, out var caseAttribute))
                {
                    var candidateWithTag = caseAttribute.Tag is { } tag
                        ? candidate with
                        {
                            Tag = tag,
                            TagLocation = caseAttribute.TagLocation,
                            IsTagExplicit = true,
                        }
                        : candidate;

                    if (!string.IsNullOrWhiteSpace(caseAttribute.ParamName))
                    {
                        candidateWithTag = candidateWithTag with
                        {
                            ParamName = caseAttribute.ParamName!,
                            ParamNameLocation = caseAttribute.ParamNameLocation,
                            IsParamNameExplicit = true,
                            IsParamNameValid = caseAttribute.IsParamNameValid,
                        };
                    }

                    builder.Add(candidateWithTag);
                }
                else
                {
                    builder.Add(candidate);
                }
            }

            return builder.ToImmutable();
        }

        #endregion
    }

    private static bool TryGetCaseAttributeContexts(
        IEnumerable<AttributeData> attributes,
        CancellationToken cancellationToken,
        out ImmutableArray<TaggedUnionCaseAttributeContext> attributeContexts
    )
    {
        var builder = ImmutableArray.CreateBuilder<TaggedUnionCaseAttributeContext>();

        foreach (var attribute in attributes)
        {
            if (attribute.ConstructorArguments.Length != 3)
            {
                attributeContexts = default;

                return false;
            }

            if (attribute.ConstructorArguments[0] is not
                {
                    Kind: TypedConstantKind.Type,
                    Value: ITypeSymbol typeSymbol,
                }
                || typeSymbol is IErrorTypeSymbol
            )
            {
                attributeContexts = default;

                return false;
            }

            var paramNameValue = attribute.ConstructorArguments[1].Value;
            string? paramName = null;
            var isParamNameValid = true;

            if (paramNameValue is string value)
            {
                paramName = EscapeIdentifier(value);
                isParamNameValid = IsValidParameterName(value);
            }
            else if (paramNameValue != null)
            {
                attributeContexts = default;

                return false;
            }

            if (attribute.ConstructorArguments[2].Value is not byte tag)
            {
                attributeContexts = default;

                return false;
            }

            var tagArgument = GetCaseAttributeArgument(
                attribute,
                parameterName: "tag",
                parameterIndex: 2,
                cancellationToken
            );

            builder.Add(new TaggedUnionCaseAttributeContext(
                TypeSymbol: typeSymbol,
                TypeLocation: GetCaseAttributeTypeLocation(attribute, cancellationToken),
                ParamName: paramName,
                ParamNameLocation: GetCaseAttributeParamNameLocation(attribute, cancellationToken),
                IsParamNameValid: isParamNameValid,
                Tag: tagArgument != null ? tag : null,
                TagLocation: tagArgument?.Expression.GetLocation() ?? Location.None
            ));
        }

        attributeContexts = builder.ToImmutable();

        return true;

        #region Local Functions
        static Location GetCaseAttributeTypeLocation(
            AttributeData attribute,
            CancellationToken cancellationToken
        )
        {
            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax;

            if (attributeSyntax?.ArgumentList is { Arguments.Count: > 0 })
            {
                return attributeSyntax.ArgumentList.Arguments[0].Expression.GetLocation();
            }

            return attributeSyntax?.GetLocation() ?? Location.None;
        }

        static Location GetCaseAttributeParamNameLocation(
            AttributeData attribute,
            CancellationToken cancellationToken
        )
        {
            var argument = GetCaseAttributeArgument(
                attribute,
                parameterName: "paramName",
                parameterIndex: 1,
                cancellationToken
            );

            return argument?.Expression.GetLocation()
                ?? attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation()
                ?? Location.None;
        }

        static AttributeArgumentSyntax? GetCaseAttributeArgument(
            AttributeData attribute,
            string parameterName,
            int parameterIndex,
            CancellationToken cancellationToken
        )
        {
            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax;
            var arguments = attributeSyntax?.ArgumentList?.Arguments;

            if (arguments == null)
            {
                return null;
            }

            var namedArgument = arguments.Value.FirstOrDefault(argument =>
                argument.NameColon?.Name.Identifier.ValueText == parameterName
            );

            if (namedArgument != null)
            {
                return namedArgument;
            }

            return arguments.Value.Count > parameterIndex
                && arguments.Value[parameterIndex].NameColon == null
                    ? arguments.Value[parameterIndex]
                    : null;
        }
        #endregion
    }

    private static ImmutableArray<UnionCaseCandidateContext> CreateUnionCaseCandidates(
        TaggedUnionInterfaceContext taggedUnionInterface
    )
    {
        var typeArguments = taggedUnionInterface.TypeSymbol.TypeArguments;
        var builder = ImmutableArray.CreateBuilder<UnionCaseCandidateContext>(typeArguments.Length);

        for (var i = 0; i < typeArguments.Length; i++)
        {
            var typeSymbol = typeArguments[i];
            var typeLocation = taggedUnionInterface.TypeArgumentLocations[i];

            builder.Add(new UnionCaseCandidateContext(
                TypeLocation: typeLocation,
                TypeSymbol: typeSymbol,
                ParamName: GetCaseParamName(typeSymbol),
                ParamNameLocation: typeLocation,
                IsParamNameExplicit: false,
                IsParamNameValid: true,
                Tag: (byte)(i + 1),
                TagLocation: typeLocation,
                IsTagExplicit: false
            ));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<TaggedUnionInterfaceContext> GetTaggedUnionInterfaceContexts(
        INamedTypeSymbol targetTypeSymbol,
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var interfaceDefinitions = Enumerable
            .Range(2, 7)
            .Select(arity => compilation.GetTypeByMetadataName(
                $"{TaggedUnionMetadataNames.TaggedUnionInterface}`{arity}"
            ))
            .Where(static symbol => symbol != null)
            .Select(static symbol => symbol!)
            .ToImmutableHashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var builder = ImmutableArray.CreateBuilder<TaggedUnionInterfaceContext>();

        foreach (var interfaceSymbol in targetTypeSymbol.Interfaces)
        {
            if (!interfaceDefinitions.Contains(interfaceSymbol.OriginalDefinition))
            {
                continue;
            }

            var interfaceSyntax = FindInterfaceSyntax(
                targetTypeSymbol,
                interfaceSymbol,
                compilation,
                cancellationToken
            );
            var location = interfaceSyntax?.GetLocation() ?? targetTypeSymbol.Locations.FirstOrDefault() ?? Location.None;
            var typeArgumentLocations = GetTypeArgumentLocations(
                interfaceSyntax,
                interfaceSymbol.Arity,
                location
            );

            builder.Add(new TaggedUnionInterfaceContext(
                TypeSymbol: interfaceSymbol,
                Location: location,
                TypeArgumentLocations: typeArgumentLocations
            ));
        }

        return builder.ToImmutable();

        #region Local Functions
        static BaseTypeSyntax? FindInterfaceSyntax(
            INamedTypeSymbol targetTypeSymbol,
            INamedTypeSymbol interfaceSymbol,
            Compilation compilation,
            CancellationToken cancellationToken
        )
        {
            foreach (var syntaxReference in targetTypeSymbol.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax(cancellationToken) is not StructDeclarationSyntax
                {
                    BaseList: { } baseList,
                })
                {
                    continue;
                }

                var semanticModel = compilation.GetSemanticModel(baseList.SyntaxTree);

                foreach (var baseType in baseList.Types)
                {
                    var baseTypeSymbol = semanticModel.GetTypeInfo(baseType.Type, cancellationToken).Type;

                    if (SymbolEqualityComparer.Default.Equals(baseTypeSymbol, interfaceSymbol))
                    {
                        return baseType;
                    }
                }
            }

            return null;
        }

        static ImmutableArray<Location> GetTypeArgumentLocations(
            BaseTypeSyntax? interfaceSyntax,
            int arity,
            Location fallbackLocation
        )
        {
            var genericName = interfaceSyntax?
                .Type
                .DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .LastOrDefault(node =>
                    node.Identifier.ValueText == "ITaggedUnion"
                    && node.TypeArgumentList.Arguments.Count == arity
                );

            if (genericName == null)
            {
                return Enumerable.Repeat(fallbackLocation, arity).ToImmutableArray();
            }

            return genericName
                .TypeArgumentList
                .Arguments
                .Select(static argument => argument.GetLocation())
                .ToImmutableArray();
        }
        #endregion
    }

    private static bool ValidateTargetTypeDeclaration(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<UnionAnalysisError>.Builder errorsBuilder
    )
    {
        var modifiers = structDeclarationSyntax.Modifiers;
        var targetTypeName = structDeclarationSyntax.Identifier.ToString();

        if (!modifiers.Any(ReadOnlyKeyword) || !modifiers.Any(PartialKeyword))
        {
            errorsBuilder.Add(new UnionAnalysisError.TargetTypeMustBeReadOnlyPartialStruct(
                Location: structDeclarationSyntax.GetLocation(),
                TargetTypeName: targetTypeName
            ));

            return false;
        }

        if (structDeclarationSyntax.TypeParameterList != null)
        {
            errorsBuilder.Add(new UnionAnalysisError.TargetTypeMustBeNonGeneric(
                Location: structDeclarationSyntax.GetLocation(),
                TargetTypeName: targetTypeName
            ));

            return false;
        }

        return true;
    }

    private static bool ValidateTargetTypeMembers(
        StructDeclarationSyntax structDeclarationSyntax,
        INamedTypeSymbol typeSymbol,
        ImmutableArray<UnionAnalysisError>.Builder errorsBuilder,
        CancellationToken cancellationToken
    )
    {
        var userDefinedConstructorSymbols = typeSymbol
            .InstanceConstructors
            .Where(x => !x.IsImplicitlyDeclared)
            .ToImmutableArray();

        if (userDefinedConstructorSymbols.Length > 0)
        {
            foreach (var syntaxReference in userDefinedConstructorSymbols.SelectMany(x => x.DeclaringSyntaxReferences))
            {
                var syntax = (ConstructorDeclarationSyntax)syntaxReference.GetSyntax(cancellationToken);

                errorsBuilder.Add(new UnionAnalysisError.TargetTypeCannotDeclareInstanceConstructors(
                    Location: syntax.Identifier.GetLocation(),
                    TargetTypeName: structDeclarationSyntax.Identifier.ToString()
                ));
            }

            return false;
        }

        return true;
    }

    private static bool ValidateCaseAttributes(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<UnionCaseCandidateContext> candidates,
        ImmutableArray<TaggedUnionCaseAttributeContext> attributes,
        ImmutableArray<UnionAnalysisError>.Builder errorsBuilder
    )
    {
        var knownTypes = candidates
            .Select(x => x.TypeSymbol)
            .ToImmutableHashSet(SymbolEqualityComparer.Default);
        var unmatchedAttributeContexts = attributes
            .Where(x => !knownTypes.Contains(x.TypeSymbol))
            .ToImmutableArray();

        foreach (var context in unmatchedAttributeContexts)
        {
            errorsBuilder.Add(new UnionAnalysisError.CaseAttributeTypeMustMatchCaseType(
                Location: context.TypeLocation,
                CaseTypeName: context.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                TargetTypeName: structDeclarationSyntax.Identifier.ToString()
            ));
        }

        return unmatchedAttributeContexts.Length == 0;
    }

    private static bool ValidateCaseTypes(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<UnionCaseCandidateContext> candidates,
        ImmutableArray<UnionAnalysisError>.Builder errorsBuilder
    )
    {
        var unsupportedCaseTypes = new List<UnionCaseCandidateContext>();
        var duplicateCaseTypes = new List<UnionCaseCandidateContext>();
        var invalidCaseParameterNames = new List<UnionCaseCandidateContext>();
        var duplicateCaseParameterNames = new List<UnionCaseCandidateContext>();
        var knownTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var knownParameterNames = new Dictionary<string, UnionCaseCandidateContext>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            var typeSymbol = candidate.TypeSymbol;
            var isSupportedCaseType = IsSupportedCaseType(typeSymbol);
            var isDuplicateType = !knownTypes.Add(typeSymbol);

            if (!isSupportedCaseType)
            {
                unsupportedCaseTypes.Add(candidate);
            }
            else if (isDuplicateType)
            {
                duplicateCaseTypes.Add(candidate);
            }
            else if (!candidate.IsParamNameValid)
            {
                invalidCaseParameterNames.Add(candidate);
            }
            else if (knownParameterNames.TryGetValue(candidate.ParamName, out var existingCandidate))
            {
                duplicateCaseParameterNames.Add(GetDuplicateCaseParameterNameErrorCandidate(
                    existingCandidate,
                    candidate
                ));
            }
            else
            {
                knownParameterNames.Add(candidate.ParamName, candidate);
            }
        }

        foreach (var candidate in unsupportedCaseTypes)
        {
            errorsBuilder.Add(new UnionAnalysisError.UnsupportedCaseType(
                Location: candidate.TypeLocation,
                CaseTypeName: candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                TargetTypeName: structDeclarationSyntax.Identifier.ToString()
            ));
        }

        foreach (var candidate in duplicateCaseTypes)
        {
            errorsBuilder.Add(new UnionAnalysisError.DuplicateCaseType(
                Location: candidate.TypeLocation,
                CaseTypeName: candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                TargetTypeName: structDeclarationSyntax.Identifier.ToString()
            ));
        }

        foreach (var candidate in invalidCaseParameterNames)
        {
            errorsBuilder.Add(new UnionAnalysisError.InvalidCaseParameterName(
                Location: candidate.ParamNameLocation,
                CaseTypeName: candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                ParameterName: candidate.ParamName,
                TargetTypeName: structDeclarationSyntax.Identifier.ToString()
            ));
        }

        foreach (var candidate in duplicateCaseParameterNames)
        {
            errorsBuilder.Add(new UnionAnalysisError.DuplicateCaseParameterName(
                Location: candidate.ParamNameLocation,
                CaseTypeName: candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                ParameterName: candidate.ParamName,
                TargetTypeName: structDeclarationSyntax.Identifier.ToString()
            ));
        }

        return unsupportedCaseTypes.Count == 0
            && duplicateCaseTypes.Count == 0
            && invalidCaseParameterNames.Count == 0
            && duplicateCaseParameterNames.Count == 0;

        #region Local Functions
        static bool IsSupportedCaseType(ITypeSymbol typeSymbol)
        {
            if (typeSymbol.NullableAnnotation == NullableAnnotation.Annotated)
            {
                return false;
            }

            return typeSymbol switch
            {
                INamedTypeSymbol namedTypeSymbol => namedTypeSymbol is
                {
                    IsRefLikeType: false,
                    IsUnboundGenericType: false,
                    OriginalDefinition.SpecialType: not (System_Object or System_Void or System_Nullable_T),
                },
                IArrayTypeSymbol => true,
                _ => false,
            };
        }

        static UnionCaseCandidateContext GetDuplicateCaseParameterNameErrorCandidate(
            UnionCaseCandidateContext existingCandidate,
            UnionCaseCandidateContext candidate
        )
        {
            return candidate.IsParamNameExplicit || !existingCandidate.IsParamNameExplicit
                ? candidate
                : existingCandidate;
        }
        #endregion
    }

    private static bool ValidateCaseTags(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<UnionCaseCandidateContext> candidates,
        ImmutableArray<UnionAnalysisError>.Builder errorsBuilder
    )
    {
        var invalidCaseTags = new List<UnionCaseCandidateContext>();
        var duplicateCaseTags = new List<UnionCaseCandidateContext>();
        var knownTags = new Dictionary<byte, UnionCaseCandidateContext>();

        foreach (var candidate in candidates)
        {
            if (candidate.Tag == 0)
            {
                invalidCaseTags.Add(candidate);

                continue;
            }

            if (knownTags.TryGetValue(candidate.Tag, out var existingCandidate))
            {
                duplicateCaseTags.Add(GetDuplicateCaseTagErrorCandidate(
                    existingCandidate,
                    candidate
                ));
            }
            else
            {
                knownTags.Add(candidate.Tag, candidate);
            }
        }

        foreach (var candidate in invalidCaseTags)
        {
            errorsBuilder.Add(new UnionAnalysisError.InvalidCaseTag(
                Location: candidate.TagLocation,
                CaseTypeName: candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                Tag: candidate.Tag,
                TargetTypeName: structDeclarationSyntax.Identifier.ToString()
            ));
        }

        foreach (var candidate in duplicateCaseTags)
        {
            errorsBuilder.Add(new UnionAnalysisError.DuplicateCaseTag(
                Location: candidate.TagLocation,
                CaseTypeName: candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                Tag: candidate.Tag,
                TargetTypeName: structDeclarationSyntax.Identifier.ToString()
            ));
        }

        return invalidCaseTags.Count == 0 && duplicateCaseTags.Count == 0;

        #region Local Functions
        static UnionCaseCandidateContext GetDuplicateCaseTagErrorCandidate(
            UnionCaseCandidateContext existingCandidate,
            UnionCaseCandidateContext candidate
        )
        {
            return candidate.IsTagExplicit || !existingCandidate.IsTagExplicit
                ? candidate
                : existingCandidate;
        }
        #endregion
    }

    private static string GetCaseTypeName(ITypeSymbol typeSymbol)
    {
        return typeSymbol.ToDisplayString(FullyQualifiedFormat.WithMiscellaneousOptions(
            UseSpecialTypes
            | IncludeNullableReferenceTypeModifier
        ));
    }

    private static string GetCaseParamName(ITypeSymbol typeSymbol)
    {
        var originalName = GetCaseParamBaseName(typeSymbol);

        if (originalName.Length > 2
            && typeSymbol.TypeKind == Interface
            && originalName[0] == 'I'
            && char.IsUpper(originalName[1])
        )
        {
            originalName = originalName[1..];
        }

        return EscapeIdentifier(GetCamelCaseName(originalName));

        #region Local Functions

        static string GetCaseParamBaseName(ITypeSymbol typeSymbol)
        {
            return typeSymbol switch
            {
                INamedTypeSymbol
                {
                    OriginalDefinition.SpecialType: System_Nullable_T,
                    TypeArguments.Length: 1,
                } namedTypeSymbol
                    => GetCaseParamBaseName(namedTypeSymbol.TypeArguments[0]),

                INamedTypeSymbol
                {
                    SpecialType: not SpecialType.None,
                } namedTypeSymbol
                    => ToDisplayString(namedTypeSymbol),

                INamedTypeSymbol namedTypeSymbol
                    => namedTypeSymbol.Name,

                IArrayTypeSymbol arrayTypeSymbol
                    => GetCaseParamBaseName(arrayTypeSymbol.ElementType) + "Array",

                _
                    => ToDisplayString(typeSymbol),
            };
        }

        static string ToDisplayString(ITypeSymbol typeSymbol)
        {
            return typeSymbol.ToDisplayString(MinimallyQualifiedFormat.WithMiscellaneousOptions(UseSpecialTypes));
        }

        #endregion
    }
    #endregion
}
