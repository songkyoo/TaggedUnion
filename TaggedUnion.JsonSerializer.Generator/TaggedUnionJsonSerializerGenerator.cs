using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Macaron.Union.TaggedUnionMetadataNames;

namespace Macaron.Union;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionJsonSerializerGenerator : IIncrementalGenerator
{
    #region Constants
    private const string TaggedUnionJsonSerializerAttributeMetadataName = "Macaron.Union.TaggedUnionJsonSerializerAttribute";
    #endregion

    #region Static Methods
    private static AnalysisResult? CreateUnionContext(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var taggedUnionAttribute = context
            .TargetSymbol
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == TaggedUnionAttribute);

        return taggedUnionAttribute != null
            ? UnionGenerationModelFactory.Create(context, taggedUnionAttribute, cancellationToken)
            : null;
    }

    private static SourceText GenerateSourceText(UnionGenerationModel model)
    {
        var writer = new TaggedUnionJsonSerializerSourceWriter(model);
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
                fullyQualifiedMetadataName: TaggedUnionJsonSerializerAttributeMetadataName,
                predicate: static (syntaxNode, _) => syntaxNode is StructDeclarationSyntax,
                transform: CreateUnionContext
            )
            .Where(static result => result != null)
            .Select(static (result, _) => result!);

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
        context.RegisterSourceOutput(
            source: analysisResultProvider
                .Where(x => x is AnalysisResult.Success)
                .Select((x, _) => ((AnalysisResult.Success)x).Model),
            static (sourceProductionContext, model) =>
            {
                var hintName = $"{model.HintName}.JsonSerializer.g.cs";
                var sourceText = GenerateSourceText(model);

                sourceProductionContext.AddSource(hintName, sourceText);
            }
        );
    }
    #endregion
}
