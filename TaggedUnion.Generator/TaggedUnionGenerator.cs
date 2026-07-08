using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.TaggedUnion.SourceGenerationHelper;
using static Macaron.TaggedUnion.StringHelper;
using static Macaron.TaggedUnion.UnionCaseStorageKind;
using static Microsoft.CodeAnalysis.SpecialType;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;
using static Microsoft.CodeAnalysis.SymbolDisplayMiscellaneousOptions;

namespace Macaron.TaggedUnion;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionGenerator : IIncrementalGenerator
{
    #region Constants
    private const string TaggedUnionAttributeString = "Macaron.TaggedUnion.TaggedUnionAttribute";
    #endregion

    #region Static Methods
    private static bool TryGetTypeArguments(
        AttributeData attribute,
        CancellationToken cancellationToken,
        out ImmutableArray<ConstructorTypeArgumentContext> typeArgumentContexts
    )
    {
        var builder = ImmutableArray.CreateBuilder<ConstructorTypeArgumentContext>(attribute.ConstructorArguments.Length);
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
                typeArgumentContexts = default;

                return false;
            }

            builder.Add(new ConstructorTypeArgumentContext(
                Node: attributeSyntax.ArgumentList!.Arguments[i],
                Symbol: typeSymbol
            ));
        }

        typeArgumentContexts = builder.ToImmutable();

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
                descriptor: TaggedUnionDiagnostics.TargetTypeMustBeNotGenericRule,
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
                    descriptor: TaggedUnionDiagnostics.UserDefinedConstructorNotAllowedRule,
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
        ImmutableArray<ConstructorTypeArgumentContext> contexts,
        ImmutableArray<Diagnostic>.Builder diagnosticsBuilder
    )
    {
        var notAllowedTypes = new List<ConstructorTypeArgumentContext>();

        foreach (var context in contexts)
        {
            var (_, typeSymbol) = context;

            switch (typeSymbol)
            {
                case INamedTypeSymbol namedTypeSymbol:
                {
                    if (namedTypeSymbol.SpecialType is System_Void or System_Object
                        || namedTypeSymbol.IsUnboundGenericType
                        || namedTypeSymbol.IsRefLikeType
                    )
                    {
                        notAllowedTypes.Add(context);
                    }

                    break;
                }
                case IArrayTypeSymbol:
                {
                    break;
                }
                default:
                {
                    notAllowedTypes.Add(context);

                    break;
                }
            }
        }

        foreach (var (syntaxNode, typeSymbol) in notAllowedTypes)
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionDiagnostics.NotAllowedCaseTypeRule,
                location: syntaxNode.GetLocation(),
                messageArgs:
                [
                    typeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                    structDeclarationSyntax.Identifier,
                ]
            ));
        }

        return notAllowedTypes.Count == 0;
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

    private static string GetHintName(INamedTypeSymbol typeSymbol)
    {
        var assemblyName = typeSymbol.ContainingAssembly != null ? $"{typeSymbol.ContainingAssembly}," : "";
        var qualifiedName = typeSymbol.ToDisplayString(FullyQualifiedFormat);

        const uint fnvPrime = 16777619;
        const uint offsetBasis = 2166136261;

        var bytes = Encoding.UTF8.GetBytes($"{assemblyName}, {qualifiedName}");
        var hash = offsetBasis;

        foreach (var b in bytes)
        {
            hash ^= b;
            hash *= fnvPrime;
        }

        return $"{typeSymbol.Name}_{typeSymbol.Arity}.{hash:x8}.g.cs";
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: TaggedUnionAttributeString,
                predicate: static (syntaxNode, _) => syntaxNode is StructDeclarationSyntax,
                transform: static UnionValidationResult (context, cancellationToken) =>
                {
                    var taggedUnionAttribute = context.Attributes[0];

                    if (!TryGetTypeArguments(taggedUnionAttribute, cancellationToken, out var typeArgumentContexts))
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
                        typeArgumentContexts,
                        diagnosticsBuilder
                    );

                    if (!isValid)
                    {
                        return new UnionValidationResult.Failure(diagnosticsBuilder.ToImmutable());
                    }

                    var caseContextsBuilder = ImmutableArray.CreateBuilder<UnionCaseContext>();

                    for (var i = 0; i < typeArgumentContexts.Length; i++)
                    {
                        var caseTypeSymbol = typeArgumentContexts[i].Symbol;
                        var caseContext = new UnionCaseContext(
                            TypeSymbol: caseTypeSymbol,
                            StorageKind: caseTypeSymbol.IsReferenceType
                                ? Reference
                                : throw new InvalidOperationException($"Cannot determine the storage kind for type '{caseTypeSymbol.ToDisplayString()}'."),
                            Tag: i + 1,
                            FullyQualifiedTypeName: GetCaseTypeName(caseTypeSymbol),
                            ParamName: GetCaseParamName(caseTypeSymbol)
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
            );

        context.RegisterSourceOutput(results, static (sourceProductionContext, result) =>
        {
            switch (result)
            {
                case UnionValidationResult.Failure { Diagnostics: var diagnostics }:
                {
                    foreach (var diagnostic in diagnostics)
                    {
                        sourceProductionContext.ReportDiagnostic(diagnostic);
                    }

                    return;
                }
                case UnionValidationResult.Success { Context: var context }:
                {
                    var hintName = GetHintName(context.TypeSymbol);
                    var sourceText = GenerateSourceText(context);

                    sourceProductionContext.AddSource(hintName, sourceText);

                    return;
                }
            }
        });
    }
    #endregion
}
