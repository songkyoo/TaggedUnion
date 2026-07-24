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

    #region Types
    private abstract record JsonSerializerAnalysisResult
    {
        public sealed record Success(UnionGenerationModel Model) : JsonSerializerAnalysisResult;

        public sealed record Failure(Diagnostic Diagnostic) : JsonSerializerAnalysisResult;
    }
    #endregion

    #region Static Methods
    private static JsonSerializerAnalysisResult? CreateAnalysisResult(
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

            return result is AnalysisResult.Success success
                ? new JsonSerializerAnalysisResult.Success(success.Model)
                : null;
        }

        var structDeclarationSyntax = (StructDeclarationSyntax)context.TargetNode;
        var location = GetLocation(context, cancellationToken);
        var diagnostic = Diagnostic.Create(
            descriptor: TaggedUnionJsonSerializerDiagnostics.TaggedUnionAttributeRequiredRule,
            location,
            messageArgs: [structDeclarationSyntax.Identifier]
        );

        return new JsonSerializerAnalysisResult.Failure(diagnostic);

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
                .Where(static x => x is JsonSerializerAnalysisResult.Failure)
                .Select(static (x, _) => ((JsonSerializerAnalysisResult.Failure)x!).Diagnostic),
            action: static (sourceProductionContext, diagnostic) =>
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }
        );
        context.RegisterSourceOutput(
            source: analysisResultProvider
                .Where(static x => x is JsonSerializerAnalysisResult.Success)
                .Select(static (x, _) => ((JsonSerializerAnalysisResult.Success)x!).Model)
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
