using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.Union.StringHelper;
using static Macaron.Union.UnionCaseStorageKind;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;
using static Microsoft.CodeAnalysis.SpecialType;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;
using static Microsoft.CodeAnalysis.TypeKind;

namespace Macaron.Union;

internal static class UnionContextFactory
{
    #region Constants
    private static readonly string TaggedUnionCaseAttributeString = "Macaron.Union.TaggedUnionCaseAttribute";
    private static readonly string AttributeString = "System.Attribute";
    private static readonly string UnionAttributeString = "System.Runtime.CompilerServices.UnionAttribute";
    private static readonly string UnionInterfaceString = "System.Runtime.CompilerServices.IUnion";
    #endregion

    #region Types
    private sealed record TaggedUnionCaseAttributeContext(
        ITypeSymbol TypeSymbol,
        Location TypeLocation,
        string? ParamName,
        Location ParamNameLocation,
        bool IsParamNameValid
    );
    #endregion

    #region Static Methods
    public static UnionValidationResult Create(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var taggedUnionAttribute = context.Attributes[0];

        if (!TryGetUnionCaseCandidates(taggedUnionAttribute, cancellationToken, out var caseCandidates))
        {
            return new UnionValidationResult.Failure(ImmutableArray<Diagnostic>.Empty);
        }

        var structDeclarationSyntax = (StructDeclarationSyntax)context.TargetNode;
        var targetTypeSymbol = context.SemanticModel.GetDeclaredSymbol(
            structDeclarationSyntax,
            cancellationToken
        );

        if (targetTypeSymbol == null)
        {
            return new UnionValidationResult.Failure(ImmutableArray<Diagnostic>.Empty);
        }

        if (!TryGetCaseAttributeContexts(
            attributes: context
                .TargetSymbol
                .GetAttributes()
                .Where(x => x.AttributeClass?.ToDisplayString() == TaggedUnionCaseAttributeString),
            cancellationToken,
            out var caseAttributes
        ))
        {
            return new UnionValidationResult.Failure(ImmutableArray<Diagnostic>.Empty);
        }

        var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();
        var isValid = true;

        isValid &= ValidateTargetTypeDeclaration(
            structDeclarationSyntax,
            diagnosticsBuilder
        );
        isValid &= ValidateTargetTypeMembers(
            structDeclarationSyntax,
            targetTypeSymbol,
            diagnosticsBuilder,
            cancellationToken
        );
        isValid &= ValidateCaseAttributes(
            structDeclarationSyntax,
            caseCandidates,
            caseAttributes,
            diagnosticsBuilder
        );

        caseCandidates = ApplyCaseAttributeParamNames(
            caseCandidates,
            attributeMap: CreateCaseAttributeMap(caseAttributes)
        );

        isValid &= ValidateCaseTypes(
            structDeclarationSyntax,
            caseCandidates,
            diagnosticsBuilder
        );

        if (!isValid)
        {
            return new UnionValidationResult.Failure(diagnosticsBuilder.ToImmutable());
        }

        var caseContextsBuilder = ImmutableArray.CreateBuilder<UnionCaseContext>();

        for (var i = 0; i < caseCandidates.Length; i++)
        {
            var unionCaseCandidate = caseCandidates[i];
            var caseTypeSymbol = unionCaseCandidate.TypeSymbol;
            var caseContext = new UnionCaseContext(
                StorageKind: GetCaseStorageKind(caseTypeSymbol),
                Tag: i + 1,
                FullyQualifiedTypeName: GetCaseTypeName(caseTypeSymbol),
                ParamName: unionCaseCandidate.ParamName
            );

            caseContextsBuilder.Add(caseContext);
        }

        var unionTypeSymbol = (INamedTypeSymbol)context.TargetSymbol;
        var unionContext = new UnionContext(
            TypeSymbol: unionTypeSymbol,
            TypeName: unionTypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
            SupportsOfficialUnion: SupportsOfficialUnion(context.SemanticModel.Compilation),
            CaseContexts: caseContextsBuilder.ToImmutable()
        );

        return new UnionValidationResult.Success(unionContext);

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
                if (string.IsNullOrWhiteSpace(attribute.ParamName))
                {
                    continue;
                }

                taggedUnionCaseAttributeMapBuilder[attribute.TypeSymbol] = attribute;
            }

            return taggedUnionCaseAttributeMapBuilder.ToImmutable();
        }

        static ImmutableArray<UnionCaseCandidateContext> ApplyCaseAttributeParamNames(
            ImmutableArray<UnionCaseCandidateContext> candidates,
            ImmutableDictionary<ITypeSymbol, TaggedUnionCaseAttributeContext> attributeMap
        )
        {
            var builder = ImmutableArray.CreateBuilder<UnionCaseCandidateContext>(candidates.Length);

            foreach (var unionCaseCandidate in candidates)
            {
                if (attributeMap.TryGetValue(unionCaseCandidate.TypeSymbol, out var caseAttribute))
                {
                    builder.Add(unionCaseCandidate with
                    {
                        ParamName = caseAttribute.ParamName!,
                        ParamNameLocation = caseAttribute.ParamNameLocation,
                        IsParamNameExplicit = true,
                        IsParamNameValid = caseAttribute.IsParamNameValid,
                    });
                }
                else
                {
                    builder.Add(unionCaseCandidate);
                }
            }

            return builder.ToImmutable();
        }

        static UnionCaseStorageKind GetCaseStorageKind(ITypeSymbol typeSymbol)
        {
            return typeSymbol switch
            {
                { IsReferenceType: true } => Reference,
                { IsUnmanagedType: true } => Unmanaged,
                { IsValueType: true } => Managed,
                _ => throw new InvalidOperationException($"Cannot determine the storage kind for type '{typeSymbol.ToDisplayString()}'."),
            };
        }
        #endregion
    }

    private static bool SupportsOfficialUnion(Compilation compilation)
    {
        var unionAttributeSymbol = compilation.GetTypeByMetadataName(UnionAttributeString);
        var unionInterfaceSymbol = compilation.GetTypeByMetadataName(UnionInterfaceString);

        return IsUnionAttribute(unionAttributeSymbol, compilation)
            && IsUnionInterface(unionInterfaceSymbol, compilation);

        #region Local Functions
        static bool IsUnionAttribute(INamedTypeSymbol? symbol, Compilation compilation)
        {
            if (symbol is not { TypeKind: Class, Arity: 0 })
            {
                return false;
            }

            var attributeSymbol = compilation.GetTypeByMetadataName(AttributeString);
            var currentSymbol = symbol;

            while (currentSymbol != null)
            {
                if (SymbolEqualityComparer.Default.Equals(currentSymbol, attributeSymbol))
                {
                    return true;
                }

                currentSymbol = currentSymbol.BaseType;
            }

            return false;
        }

        static bool IsUnionInterface(INamedTypeSymbol? symbol, Compilation compilation)
        {
            if (symbol is not { TypeKind: Interface, Arity: 0 })
            {
                return false;
            }

            var objectSymbol = compilation.GetSpecialType(System_Object);

            return symbol
                .GetMembers("Value")
                .OfType<IPropertySymbol>()
                .Any(propertySymbol =>
                    propertySymbol.Parameters.Length == 0
                    && propertySymbol.GetMethod is { DeclaredAccessibility: Accessibility.Public }
                    && SymbolEqualityComparer.Default.Equals(propertySymbol.Type, objectSymbol)
                );
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
            if (attribute.ConstructorArguments.Length < 1
                || attribute.ConstructorArguments[0] is not
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

            string? paramName = null;
            var isParamNameValid = true;

            if (attribute.ConstructorArguments.Length > 1)
            {
                var paramNameValue = attribute.ConstructorArguments[1].Value;

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
            }

            builder.Add(new TaggedUnionCaseAttributeContext(
                TypeSymbol: typeSymbol,
                TypeLocation: GetCaseAttributeTypeLocation(attribute, cancellationToken),
                ParamName: paramName,
                ParamNameLocation: GetCaseAttributeParamNameLocation(attribute, cancellationToken),
                IsParamNameValid: isParamNameValid
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
            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax;

            if (attributeSyntax?.ArgumentList is { Arguments.Count: > 1 })
            {
                return attributeSyntax.ArgumentList.Arguments[1].Expression.GetLocation();
            }

            return attributeSyntax?.GetLocation() ?? Location.None;
        }
        #endregion
    }

    private static bool TryGetUnionCaseCandidates(
        AttributeData attribute,
        CancellationToken cancellationToken,
        out ImmutableArray<UnionCaseCandidateContext> candidateContexts
    )
    {
        var builder = ImmutableArray.CreateBuilder<UnionCaseCandidateContext>(attribute.ConstructorArguments.Length);
        var attributeSyntax = (AttributeSyntax)attribute.ApplicationSyntaxReference?.GetSyntax(cancellationToken)!;
        var constructorArguments = attribute.ConstructorArguments;

        for (var i = 0; i < constructorArguments.Length; i++)
        {
            var typedConstant = constructorArguments[i];

            if (typedConstant.Kind != TypedConstantKind.Type
                || typedConstant.Value is not ITypeSymbol typeSymbol
                || typeSymbol is IErrorTypeSymbol
            )
            {
                candidateContexts = default;

                return false;
            }

            builder.Add(new UnionCaseCandidateContext(
                ArgumentSyntax: attributeSyntax.ArgumentList!.Arguments[i],
                TypeSymbol: typeSymbol,
                ParamName: GetCaseParamName(typeSymbol),
                ParamNameLocation: attributeSyntax.ArgumentList!.Arguments[i].GetLocation(),
                IsParamNameExplicit: false,
                IsParamNameValid: true
            ));
        }

        candidateContexts = builder.ToImmutable();

        return true;
    }

    private static bool ValidateTargetTypeDeclaration(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder
    )
    {
        var modifiers = structDeclarationSyntax.Modifiers;

        if (!modifiers.Any(ReadOnlyKeyword) || !modifiers.Any(PartialKeyword))
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.TargetTypeMustBeReadOnlyPartialStructRule,
                location: structDeclarationSyntax.GetLocation(),
                messageArgs: [structDeclarationSyntax.Identifier]
            ));

            return false;
        }

        if (structDeclarationSyntax.TypeParameterList != null)
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.TargetTypeMustBeNonGenericRule,
                location: structDeclarationSyntax.GetLocation(),
                messageArgs: [structDeclarationSyntax.Identifier]
            ));

            return false;
        }

        return true;
    }

    private static bool ValidateTargetTypeMembers(
        StructDeclarationSyntax structDeclarationSyntax,
        INamedTypeSymbol typeSymbol,
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder,
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

                diagnosticsBuilder.Add(Diagnostic.Create(
                    descriptor: TaggedUnionDiagnostics.TargetTypeCannotDeclareInstanceConstructorsRule,
                    location: syntax.Identifier.GetLocation(),
                    messageArgs: [structDeclarationSyntax.Identifier]
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
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder
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
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.CaseAttributeTypeMustMatchCaseTypeRule,
                location: context.TypeLocation,
                messageArgs:
                [
                    context.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                    structDeclarationSyntax.Identifier,
                ]
            ));
        }

        return unmatchedAttributeContexts.Length == 0;
    }

    private static bool ValidateCaseTypes(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<UnionCaseCandidateContext> candidates,
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder
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
                duplicateCaseParameterNames.Add(GetDuplicateCaseParameterNameDiagnosticCandidate(
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
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.UnsupportedCaseTypeRule,
                location: candidate.ArgumentSyntax.GetLocation(),
                messageArgs:
                [
                    candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                    structDeclarationSyntax.Identifier,
                ]
            ));
        }

        foreach (var candidate in duplicateCaseTypes)
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.DuplicateCaseTypeRule,
                location: candidate.ArgumentSyntax.GetLocation(),
                messageArgs:
                [
                    candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                    structDeclarationSyntax.Identifier,
                ]
            ));
        }

        foreach (var candidate in invalidCaseParameterNames)
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.InvalidCaseParameterNameRule,
                location: candidate.ParamNameLocation,
                messageArgs:
                [
                    candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                    candidate.ParamName,
                    structDeclarationSyntax.Identifier,
                ]
            ));
        }

        foreach (var candidate in duplicateCaseParameterNames)
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.DuplicateCaseParameterNameRule,
                location: candidate.ParamNameLocation,
                messageArgs:
                [
                    candidate.TypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                    candidate.ParamName,
                    structDeclarationSyntax.Identifier,
                ]
            ));
        }

        return unsupportedCaseTypes.Count == 0
            && duplicateCaseTypes.Count == 0
            && invalidCaseParameterNames.Count == 0
            && duplicateCaseParameterNames.Count == 0;

        #region Local Functions
        static bool IsSupportedCaseType(ITypeSymbol typeSymbol)
        {
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

        static UnionCaseCandidateContext GetDuplicateCaseParameterNameDiagnosticCandidate(
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
