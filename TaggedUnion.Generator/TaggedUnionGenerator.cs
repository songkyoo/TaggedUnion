using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Macaron.Union.TaggedUnionMetadataNames;

namespace Macaron.Union;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionGenerator : IIncrementalGenerator
{
    #region Static Methods
    private static SourceText GenerateSourceText(UnionGenerationModel model)
    {
        var writer = new TaggedUnionSourceWriter(model);
        var source = writer.Generate();

        return SourceText.From(source, Encoding.UTF8);
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analysisResultProvider = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: TaggedUnionAttribute,
                predicate: (syntaxNode, _) => syntaxNode is StructDeclarationSyntax,
                transform: (attributeContext, cancellationToken) => UnionGenerationModelFactory.Create(
                    context: attributeContext,
                    taggedUnionAttribute: attributeContext.Attributes[0],
                    cancellationToken
                )
            );

        context.RegisterSourceOutput(
            source: analysisResultProvider
                .Where(x => x is AnalysisResult.Failure)
                .Select((x, _) => ((AnalysisResult.Failure)x).Diagnostics),
            action: static (sourceProductionContext, diagnostics) =>
            {
                foreach (var diagnostic in diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }
            }
        );

        var modelProvider = analysisResultProvider
            .Where(static x => x is AnalysisResult.Success)
            .Select(static (x, _) => ((AnalysisResult.Success)x).Model)
            .WithComparer(UnionGenerationModelComparer.Instance)
            .WithTrackingName(nameof(UnionGenerationModel));

        context.RegisterSourceOutput(
            source: modelProvider,
            static (sourceProductionContext, model) =>
            {
                var hintName = $"{model.HintName}.g.cs";
                var sourceText = GenerateSourceText(model);

                sourceProductionContext.AddSource(hintName, sourceText);
            }
        );
    }
    #endregion
}
