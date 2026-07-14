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
    private static Diagnostic? CreateMissingTaggedUnionAttributeDiagnostic(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.TargetSymbol.GetAttributes().Any(attribute =>
            {
                return attribute.AttributeClass?.ToDisplayString() == TaggedUnionAttribute;
            })
        )
        {
            return null;
        }

        var structDeclarationSyntax = (StructDeclarationSyntax)context.TargetNode;
        var location = GetLocation(context, cancellationToken);

        return Diagnostic.Create(
            descriptor: TaggedUnionDiagnostics.CaseAttributeRequiresTaggedUnionAttributeRule,
            location,
            messageArgs: [structDeclarationSyntax.Identifier]
        );

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
            )
            .WithTrackingName(nameof(AnalysisResult));

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
                .Where(static x => x is AnalysisResult.Success)
                .Select(static (x, _) => ((AnalysisResult.Success)x).Model)
                .WithTrackingName(nameof(UnionGenerationModel)),
            static (sourceProductionContext, model) =>
            {
                var hintName = $"{model.HintName}.g.cs";
                var sourceText = GenerateSourceText(model);

                sourceProductionContext.AddSource(hintName, sourceText);
            }
        );

        // TaggedUnionCase만 있는 경우의 진단
        var missingTaggedUnionAttributeDiagnosticProvider = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: TaggedUnionCaseAttribute,
                predicate: static (syntaxNode, _) => syntaxNode is StructDeclarationSyntax,
                transform: CreateMissingTaggedUnionAttributeDiagnostic
            )
            .Where(static diagnostic => diagnostic != null)
            .Select(static (diagnostic, _) => diagnostic!);

        context.RegisterSourceOutput(
            source: missingTaggedUnionAttributeDiagnosticProvider,
            action: static (sourceProductionContext, diagnostic) =>
                sourceProductionContext.ReportDiagnostic(diagnostic)
        );
    }
    #endregion
}
