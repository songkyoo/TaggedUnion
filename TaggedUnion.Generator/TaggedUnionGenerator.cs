using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.TaggedUnion.SourceGenerationHelper;
using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.TaggedUnion;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionGenerator : IIncrementalGenerator
{
    #region Constants
    private const string TaggedUnionAttributeString = "Macaron.TaggedUnion.TaggedUnionAttribute";
    #endregion

    #region Static Methods
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
                transform: UnionContextFactory.Create
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
