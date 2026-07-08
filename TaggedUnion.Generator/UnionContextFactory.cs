using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.Union.StringHelper;
using static Macaron.Union.UnionCaseStorageKind;
using static Microsoft.CodeAnalysis.SpecialType;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.Union;

internal static class UnionContextFactory
{
    #region Static Methods
    public static UnionValidationResult Create(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var taggedUnionAttribute = context.Attributes[0];

        if (!TryGetUnionCaseCandidates(taggedUnionAttribute, cancellationToken, out var unionCaseCandidates))
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
        isValid &= ValidateCaseTypes(
            structDeclarationSyntax,
            unionCaseCandidates,
            diagnosticsBuilder
        );

        if (!isValid)
        {
            return new UnionValidationResult.Failure(diagnosticsBuilder.ToImmutable());
        }

        var caseContextsBuilder = ImmutableArray.CreateBuilder<UnionCaseContext>();

        for (var i = 0; i < unionCaseCandidates.Length; i++)
        {
            var unionCaseCandidate = unionCaseCandidates[i];
            var caseTypeSymbol = unionCaseCandidate.TypeSymbol;
            var caseContext = new UnionCaseContext(
                TypeSymbol: caseTypeSymbol,
                StorageKind: caseTypeSymbol.IsReferenceType
                    ? Reference
                    : throw new InvalidOperationException($"Cannot determine the storage kind for type '{caseTypeSymbol.ToDisplayString()}'."),
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
            CaseContexts: caseContextsBuilder.ToImmutable()
        );

        return new UnionValidationResult.Success(unionContext);
    }

    private static bool TryGetUnionCaseCandidates(
        AttributeData attribute,
        CancellationToken cancellationToken,
        out ImmutableArray<UnionCaseCandidateContext> unionCaseCandidateContexts
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
                unionCaseCandidateContexts = default;

                return false;
            }

            builder.Add(new UnionCaseCandidateContext(
                ArgumentSyntax: attributeSyntax.ArgumentList!.Arguments[i],
                TypeSymbol: typeSymbol,
                ParamName: GetCaseParamName(typeSymbol)
            ));
        }

        unionCaseCandidateContexts = builder.ToImmutable();

        return true;
    }

    private static bool ValidateTargetTypeDeclaration(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder
    )
    {
        var modifiers = structDeclarationSyntax.Modifiers;

        if (!modifiers.Any(SyntaxKind.ReadOnlyKeyword) || !modifiers.Any(SyntaxKind.PartialKeyword))
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

    private static bool ValidateCaseTypes(
        StructDeclarationSyntax structDeclarationSyntax,
        ImmutableArray<UnionCaseCandidateContext> unionCaseCandidates,
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder
    )
    {
        var unsupportedCaseTypes = new List<UnionCaseCandidateContext>();
        var duplicateCaseTypes = new List<UnionCaseCandidateContext>();
        var duplicateCaseParameterNames = new List<UnionCaseCandidateContext>();
        var knownTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var knownParameterNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var candidate in unionCaseCandidates)
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
            else if (isSupportedCaseType && !knownParameterNames.Add(candidate.ParamName))
            {
                duplicateCaseParameterNames.Add(candidate);
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

        foreach (var candidate in duplicateCaseParameterNames)
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.DuplicateCaseParameterNameRule,
                location: candidate.ArgumentSyntax.GetLocation(),
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
                    SpecialType: not (System_Void or System_Object),
                },
                IArrayTypeSymbol => true,
                _ => false,
            };
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
        return EscapeIdentifier(GetCamelCaseName(typeSymbol.Name));
    }
    #endregion
}
