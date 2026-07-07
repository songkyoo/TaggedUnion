using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.TaggedUnion.SourceGenerationHelper;
using static Macaron.TaggedUnion.StringHelper;
using static Macaron.TaggedUnion.UnionCaseStorageKind;
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
                        return new UnionValidationResult.CompilationError();
                    }

                    var caseContextsBuilder = ImmutableArray.CreateBuilder<UnionCaseContext>();

                    for (var i = 0; i < typeArgumentContexts.Length; i++)
                    {
                        var typeSymbol = typeArgumentContexts[i].Symbol;
                        var caseContext = new UnionCaseContext(
                            TypeSymbol: typeSymbol,
                            StorageKind: typeSymbol.IsReferenceType
                                ? Reference
                                : throw new InvalidOperationException($"Cannot determine the storage kind for type '{typeSymbol.ToDisplayString()}'."),
                            Tag: i + 1,
                            FullyQualifiedTypeName: GetCaseTypeName(typeSymbol),
                            ParamName: GetCaseParamName(typeSymbol)
                        );

                        caseContextsBuilder.Add(caseContext);
                    }

                    var unionTypeSymbol = (INamedTypeSymbol)context.TargetSymbol;
                    var unionContext = new UnionContext(
                        TypeSymbol: unionTypeSymbol,
                        TypeName: unionTypeSymbol.ToDisplayString(MinimallyQualifiedFormat),
                        CaseContexts: caseContextsBuilder.ToImmutable()
                    );

                    return new UnionValidationResult.Valid(unionContext);
                }
            );

        context.RegisterSourceOutput(results, static (sourceProductionContext, result) =>
        {
            switch (result)
            {
                case UnionValidationResult.CompilationError:
                {
                    return;
                }
                case UnionValidationResult.Invalid { Diagnostics: var diagnostics }:
                {
                    foreach (var diagnostic in diagnostics)
                    {
                        sourceProductionContext.ReportDiagnostic(diagnostic);
                    }

                    return;
                }
                case UnionValidationResult.Valid { Context: var context }:
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
