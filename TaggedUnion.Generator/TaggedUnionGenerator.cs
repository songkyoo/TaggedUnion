using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Macaron.Union.TaggedUnionSourceTextFactory;

namespace Macaron.Union;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionGenerator : IIncrementalGenerator
{
    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: TaggedUnionMetadataNames.TaggedUnionAttribute,
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
                    var hintName = $"{HintNameHelper.GetTypeHintName(context.TypeSymbol)}.g.cs";
                    var sourceText = GenerateSourceText(context);

                    sourceProductionContext.AddSource(hintName, sourceText);

                    return;
                }
            }
        });
    }
    #endregion
}
