using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Macaron.Union.HintNameHelper;
using static Macaron.Union.TaggedUnionMetadataNames;

namespace Macaron.Union;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionGenerator : IIncrementalGenerator
{
    #region Static Methods
    private static SourceText GenerateSourceText(UnionContext context)
    {
        var writer = new TaggedUnionSourceWriter(context);
        var source = writer.Generate();

        return SourceText.From(source, Encoding.UTF8);
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: TaggedUnionAttribute,
                predicate: static (syntaxNode, _) => syntaxNode is StructDeclarationSyntax,
                transform: static (attributeContext, cancellationToken) => UnionContextFactory.Create(
                    context: attributeContext,
                    taggedUnionAttribute: attributeContext.Attributes[0],
                    cancellationToken
                )
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
                    var hintName = $"{GetTypeHintName(context.TypeSymbol)}.g.cs";
                    var sourceText = GenerateSourceText(context);

                    sourceProductionContext.AddSource(hintName, sourceText);

                    return;
                }
            }
        });
    }
    #endregion
}
