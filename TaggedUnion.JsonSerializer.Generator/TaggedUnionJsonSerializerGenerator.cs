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
    private static AnalysisResult? CreateAnalysisResult(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var taggedUnionAttribute = context
            .TargetSymbol
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == TaggedUnionAttribute);

        if (taggedUnionAttribute != null)
        {
            var result = UnionGenerationModelFactory.Create(
                context,
                taggedUnionAttribute,
                cancellationToken
            );

            return result is AnalysisResult.Success ? result : null;
        }

        var structDeclarationSyntax = (StructDeclarationSyntax)context.TargetNode;
        var location = GetLocation(context, cancellationToken);
        var diagnostic = Diagnostic.Create(
            descriptor: TaggedUnionJsonSerializerDiagnostics.TaggedUnionAttributeRequiredRule,
            location,
            messageArgs: [structDeclarationSyntax.Identifier]
        );

        return new AnalysisResult.Failure(diagnostic);

        #region Local Functions
        static Location GetLocation(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
        {
            var applicationSyntaxReference = context.Attributes[0].ApplicationSyntaxReference;
            var location = applicationSyntaxReference?.GetSyntax(cancellationToken).GetLocation();

            return location ?? context.TargetNode.GetLocation();
        }
        #endregion
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
                transform: CreateAnalysisResult
            )
            .WithTrackingName(nameof(AnalysisResult));

        context.RegisterSourceOutput(
            source: analysisResultProvider
                .Where(static x => x is AnalysisResult.Failure)
                .Select(static (x, _) => ((AnalysisResult.Failure)x!).Diagnostics),
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
                .Where(static x => x is AnalysisResult.Success)
                .Select(static (x, _) => ((AnalysisResult.Success)x!).Model)
                .WithTrackingName(nameof(UnionGenerationModel)),
            action: static (sourceProductionContext, model) =>
            {
                var hintName = $"{model.HintName}.JsonSerializer.g.cs";
                var sourceText = GenerateSourceText(model);

                sourceProductionContext.AddSource(hintName, sourceText);
            }
        );
    }
    #endregion
}
