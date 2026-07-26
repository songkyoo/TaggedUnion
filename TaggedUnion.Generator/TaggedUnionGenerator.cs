using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Macaron.Union.TaggedUnionMetadataNames;
using static Macaron.Union.TypeDeclarationHelper;

namespace Macaron.Union;

[Generator]
public sealed class TaggedUnionGenerator : IIncrementalGenerator
{
    #region Types
    private abstract record TaggedUnionAnalysisResult
    {
        public sealed record Success(
            UnionGenerationModel Model,
            bool GenerateConversionOperators
        ) : TaggedUnionAnalysisResult;

        public sealed record Failure(
            ImmutableArray<UnionAnalysisError> Errors
        ) : TaggedUnionAnalysisResult;
    }
    #endregion

    #region Static Methods
    private static TaggedUnionAnalysisResult CreateAnalysisResult(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var taggedUnionAttribute = context.Attributes[0];
        var result = UnionGenerationModelFactory.Create(
            context,
            taggedUnionAttribute,
            cancellationToken
        );

        return result switch
        {
            AnalysisResult.Success success => new TaggedUnionAnalysisResult.Success(
                Model: success.Model,
                GenerateConversionOperators: ConversionOperatorGenerationPolicy.IsEnabled(taggedUnionAttribute)
            ),
            AnalysisResult.Failure failure => new TaggedUnionAnalysisResult.Failure(failure.Errors),
            _ => throw new InvalidOperationException($"Invalid analysis result: {result}"),
        };
    }

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

    private static SourceText GenerateSourceText(TaggedUnionSourceGenerationModel model)
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
                predicate: static (syntaxNode, _) => IsStructTypeDeclarationSyntax(syntaxNode),
                transform: CreateAnalysisResult
            )
            .WithTrackingName(nameof(AnalysisResult));
        var unionGenerationModelProvider = analysisResultProvider
            .Where(static x => x is TaggedUnionAnalysisResult.Success)
            .Select(static (x, _) =>
            {
                var success = (TaggedUnionAnalysisResult.Success)x;

                return new TaggedUnionGenerationModel(
                    Union: success.Model,
                    GenerateConversionOperators: success.GenerateConversionOperators
                );
            })
            .WithTrackingName(nameof(UnionGenerationModel));
        var supportsOfficialUnionProvider = context
            .CompilationProvider
            .Select(static (compilation, _) => OfficialUnionSupport.IsAvailable(compilation))
            .WithTrackingName(nameof(OfficialUnionSupport));
        var sourceGenerationModelProvider = unionGenerationModelProvider
            .Combine(supportsOfficialUnionProvider)
            .Select(static (pair, _) => new TaggedUnionSourceGenerationModel(
                Model: pair.Left,
                SupportsOfficialUnion: pair.Right
            ))
            .WithTrackingName(nameof(TaggedUnionSourceGenerationModel));

        context.RegisterSourceOutput(
            source: analysisResultProvider
                .Where(static x => x is TaggedUnionAnalysisResult.Failure)
                .Select(static (x, _) => ((TaggedUnionAnalysisResult.Failure)x).Errors),
            action: static (sourceProductionContext, errors) =>
            {
                foreach (var error in errors)
                {
                    sourceProductionContext.ReportDiagnostic(TaggedUnionDiagnostics.CreateDiagnostic(error));
                }
            }
        );
        context.RegisterSourceOutput(
            source: sourceGenerationModelProvider,
            action: static (sourceProductionContext, model) =>
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
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }
        );
    }
    #endregion
}
