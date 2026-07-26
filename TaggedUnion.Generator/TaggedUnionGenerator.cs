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
            bool GenerateConversionOperators,
            ImmutableArray<TaggedUnionAnalysisDiagnostic> Diagnostics
        ) : TaggedUnionAnalysisResult
        {
            #region IEquatable<Success> Interface
            public bool Equals(Success? other)
            {
                if (ReferenceEquals(this, other))
                {
                    return true;
                }

                if (other is null)
                {
                    return false;
                }

                Diagnostics.SequenceEqual(other.Diagnostics, EqualityComparer<TaggedUnionAnalysisDiagnostic>.Default);

                return EqualityComparer<UnionGenerationModel>.Default.Equals(Model, other.Model)
                    && GenerateConversionOperators == other.GenerateConversionOperators
                    && Diagnostics.SequenceEqual(
                        items: other.Diagnostics,
                        comparer: EqualityComparer<TaggedUnionAnalysisDiagnostic>.Default
                    );
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = EqualityComparer<UnionGenerationModel>.Default.GetHashCode(Model);

                    hashCode = (hashCode * 397) ^ GenerateConversionOperators.GetHashCode();

                    if (Diagnostics.IsDefault)
                    {
                        return (hashCode * 397) ^ -1;
                    }

                    foreach (var diagnostic in Diagnostics)
                    {
                        hashCode = (hashCode * 397) ^ EqualityComparer<TaggedUnionAnalysisDiagnostic>.Default.GetHashCode(diagnostic);
                    }

                    return hashCode;
                }
            }
            #endregion
        }

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
            AnalysisResult.Success success => CreateSuccessResult(
                model: success.Model,
                taggedUnionAttribute,
                cancellationToken
            ),
            AnalysisResult.Failure failure => new TaggedUnionAnalysisResult.Failure(failure.Errors),
            _ => throw new InvalidOperationException($"Invalid analysis result: {result}"),
        };

        #region Local Functions
        static TaggedUnionAnalysisResult.Success CreateSuccessResult(
            UnionGenerationModel model,
            AttributeData taggedUnionAttribute,
            CancellationToken cancellationToken
        )
        {
            var generateConversionOperators = ConversionOperatorGenerationPolicy.IsEnabled(taggedUnionAttribute);

            if (!generateConversionOperators)
            {
                return new TaggedUnionAnalysisResult.Success(
                    Model: model,
                    GenerateConversionOperators: false,
                    Diagnostics: ImmutableArray<TaggedUnionAnalysisDiagnostic>.Empty
                );
            }

            var interfaceCaseTypeNames = model
                .Cases
                .Where(static x => x.IsInterfaceType)
                .Select(static x => x.FullyQualifiedTypeName.Replace("global::", ""))
                .ToArray();

            if (interfaceCaseTypeNames.Length == 0)
            {
                return new TaggedUnionAnalysisResult.Success(
                    Model: model,
                    GenerateConversionOperators: true,
                    Diagnostics: ImmutableArray<TaggedUnionAnalysisDiagnostic>.Empty
                );
            }

            var diagnostic = new TaggedUnionAnalysisDiagnostic.ConversionOperatorsNotSupportedForInterfaceCases(
                Location: ConversionOperatorGenerationPolicy.GetOptionLocation(
                    taggedUnionAttribute,
                    cancellationToken
                ),
                TargetTypeName: model.TypeName,
                InterfaceCaseTypeNames: string.Join(", ", interfaceCaseTypeNames)
            );

            return new TaggedUnionAnalysisResult.Success(
                Model: model,
                GenerateConversionOperators: false,
                Diagnostics: ImmutableArray.Create<TaggedUnionAnalysisDiagnostic>(diagnostic)
            );
        }
        #endregion
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
            source: analysisResultProvider
                .Where(static x => x is TaggedUnionAnalysisResult.Success)
                .Select(static (x, _) => ((TaggedUnionAnalysisResult.Success)x).Diagnostics)
                .WithTrackingName(nameof(TaggedUnionAnalysisDiagnostic)),
            action: static (sourceProductionContext, diagnostics) =>
            {
                foreach (var diagnostic in diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(TaggedUnionDiagnostics.CreateDiagnostic(diagnostic));
                }
            }
        );
        context.RegisterSourceOutput(
            source: unionGenerationModelProvider
                .Combine(supportsOfficialUnionProvider)
                .Select(static (pair, _) => new TaggedUnionSourceGenerationModel(
                    Model: pair.Left,
                    SupportsOfficialUnion: pair.Right
                ))
                .WithTrackingName(nameof(TaggedUnionSourceGenerationModel)),
            action: static (sourceProductionContext, model) =>
            {
                var hintName = $"{model.HintName}.g.cs";
                var sourceText = GenerateSourceText(model);

                sourceProductionContext.AddSource(hintName, sourceText);
            }
        );

        // TaggedUnionCase만 있는 경우의 진단
        context.RegisterSourceOutput(
            source: context
                .SyntaxProvider
                .ForAttributeWithMetadataName(
                    fullyQualifiedMetadataName: TaggedUnionCaseAttribute,
                    predicate: static (syntaxNode, _) => syntaxNode is StructDeclarationSyntax,
                    transform: CreateMissingTaggedUnionAttributeDiagnostic
                )
                .Where(static diagnostic => diagnostic != null)
                .Select(static (diagnostic, _) => diagnostic!),
            action: static (sourceProductionContext, diagnostic) =>
            {
                sourceProductionContext.ReportDiagnostic(diagnostic);
            }
        );
    }
    #endregion
}
