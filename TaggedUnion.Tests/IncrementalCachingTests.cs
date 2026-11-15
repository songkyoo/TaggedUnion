using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using MemoryPack;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class IncrementalCachingTests
{
    #region Constants
    private const string AnalysisResultTrackingName = "AnalysisResult";
    private const string AnalysisDiagnosticTrackingName = "TaggedUnionAnalysisDiagnostic";
    private const string ModelTrackingName = "UnionGenerationModel";
    private const string OfficialUnionSupportTrackingName = "OfficialUnionSupport";
    private const string SourceGenerationModelTrackingName = "TaggedUnionSourceGenerationModel";
    private const string AddedMember = "public static int IgnoredByGenerationModel => 42;";

    private const string UnionSource =
        """
        namespace Macaron.Union.Tests;

        [TaggedUnion]
        public readonly partial struct Foo : ITaggedUnionCases<int, string>
        {
            /* Members */
        }
        """;
    #endregion

    #region Tests
    [Test]
    public void TaggedUnionGeneratorCachesSourceOutputWhenModelIsUnchanged()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionGenerator(),
            sourceCode: UnionSource,
            updatedSourceCode: UnionSource.Replace("/* Members */", AddedMember),
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assertOfficialUnionSupportCaching: true
        );
    }

    [TestCaseSource(nameof(SerializerGenerators))]
    public void SerializerCachesSourceOutputWhenModelIsUnchanged(
        IIncrementalGenerator generator,
        string serializerAttribute,
        Assembly[] additionalAssemblies
    )
    {
        var source = UnionSource.Replace("[TaggedUnion]", $"[TaggedUnion]\n[{serializerAttribute}]");
        var updated = source.Replace("/* Members */", AddedMember);

        AssertSourceOutputRunReasons(generator, source, updated, additionalAssemblies);
    }

    [TestCaseSource(nameof(SerializerGenerators))]
    public void SerializerCachesSourceOutputWhenConversionOperatorPolicyChanges(
        IIncrementalGenerator generator,
        string serializerAttribute,
        Assembly[] additionalAssemblies
    )
    {
        var source = UnionSource.Replace("[TaggedUnion]", $"[TaggedUnion]\n[{serializerAttribute}]");
        var updated = source.Replace("[TaggedUnion]", "[TaggedUnion(GenerateConversionOperators = true)]");

        AssertSourceOutputRunReasons(generator, source, updated, additionalAssemblies);
    }

    [Test]
    public void TaggedUnionGeneratorCachesSourceOutputWhenOnlyAnalysisDiagnosticsChange()
    {
        var source = UnionSource
            .Replace(
                "[TaggedUnion]",
                "public interface ICase { }\n\n[TaggedUnion]"
            )
            .Replace("int, string", "ICase, string");

        AssertSourceOutputRunReasons(
            generator: new TaggedUnionGenerator(),
            sourceCode: source,
            updatedSourceCode: source.Replace("[TaggedUnion]", "[TaggedUnion(GenerateConversionOperators = true)]"),
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assertOfficialUnionSupportCaching: true,
            expectAnalysisDiagnosticChange: true
        );
    }

    [Test]
    public void TaggedUnionMemoryPackSerializerGeneratorUpdatesSourceOutputWhenCaseBecomesMemoryPackable()
    {
        const string source =
            """
            namespace Macaron.Union.Tests;

            public partial class Case { }

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<Case, int>
            {
            }
            """;

        AssertSourceOutputRunReasons(
            generator: new TaggedUnionMemoryPackSerializerGenerator(),
            sourceCode: source,
            updatedSourceCode: source.Replace("public partial class Case", "[global::MemoryPack.MemoryPackable]\npublic partial class Case"),
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                typeof(MemoryPackSerializer).Assembly,
            ],
            expectModelChange: true
        );
    }

    [TestCase("int, string", "long, string", TestName = "CaseTypeChangeUpdatesSourceOutput")]
    [TestCase("[TaggedUnion]", "[TaggedUnion(StateParamName = \"context\")]", TestName = "StateParameterNameChangeUpdatesSourceOutput")]
    public void TaggedUnionGeneratorUpdatesSourceOutputWhenModelChanges(string original, string replacement)
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionGenerator(),
            sourceCode: UnionSource,
            updatedSourceCode: UnionSource.Replace(original, replacement),
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assertOfficialUnionSupportCaching: true,
            expectModelChange: true
        );
    }
    #endregion

    #region Static Methods
    private static IEnumerable<TestCaseData> SerializerGenerators()
    {
        yield return new TestCaseData(
            new TaggedUnionJsonSerializerGenerator(),
            "TaggedUnionJsonSerializer",
            new[]
            {
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionJsonSerializerAttribute).Assembly,
                typeof(JsonSerializer).Assembly,
            }
        ).SetArgDisplayNames("JSON");

        yield return new TestCaseData(
            new TaggedUnionMemoryPackSerializerGenerator(),
            "TaggedUnionMemoryPackSerializer",
            new[]
            {
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                typeof(MemoryPackSerializer).Assembly,
            }
        ).SetArgDisplayNames("MemoryPack");
    }

    private static void AssertSourceOutputRunReasons(
        IIncrementalGenerator generator,
        string sourceCode,
        string updatedSourceCode,
        Assembly[] additionalAssemblies,
        bool assertOfficialUnionSupportCaching = false,
        bool expectModelChange = false,
        bool expectAnalysisDiagnosticChange = false
    )
    {
        Assert.That(updatedSourceCode, Is.Not.EqualTo(sourceCode));

        var (compilation, driver) = Helper.CreateCompilationAndDriver(
            sourceCode,
            additionalAssemblies,
            assemblyName: "Macaron.TaggedUnion.Tests",
            generators: [generator.AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true
            )
        );
        var trackingDriver = driver.RunGenerators(compilation);
        var originalSyntaxTree = compilation.SyntaxTrees.Single();
        var updatedSyntaxTree = CSharpSyntaxTree.ParseText(
            updatedSourceCode,
            (CSharpParseOptions)originalSyntaxTree.Options
        );
        var updatedCompilation = compilation.ReplaceSyntaxTree(originalSyntaxTree, updatedSyntaxTree);

        trackingDriver = trackingDriver.RunGenerators(updatedCompilation);

        var result = trackingDriver.GetRunResult().Results.Single();
        var analysisResultReasons = GetRunReasons(result.TrackedSteps, AnalysisResultTrackingName);
        var analysisDiagnosticReasons = expectAnalysisDiagnosticChange
            ? GetRunReasons(result.TrackedSteps, AnalysisDiagnosticTrackingName)
            : [];
        var modelReasons = GetRunReasons(result.TrackedSteps, ModelTrackingName);
        var officialUnionSupportReasons = assertOfficialUnionSupportCaching
            ? GetRunReasons(result.TrackedSteps, OfficialUnionSupportTrackingName)
            : [];
        var sourceGenerationModelReasons = assertOfficialUnionSupportCaching
            ? GetRunReasons(result.TrackedSteps, SourceGenerationModelTrackingName)
            : [];
        var outputReasons = result
            .TrackedOutputSteps
            .Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(
                analysisResultReasons,
                Is.EqualTo(new[]
                {
                    expectModelChange
                        || expectAnalysisDiagnosticChange
                        ? IncrementalStepRunReason.Modified
                        : IncrementalStepRunReason.Unchanged,
                })
            );
            Assert.That(
                modelReasons,
                Is.EqualTo(new[]
                {
                    expectModelChange
                        ? IncrementalStepRunReason.Modified
                        : expectAnalysisDiagnosticChange
                            ? IncrementalStepRunReason.Unchanged
                        : IncrementalStepRunReason.Cached,
                })
            );

            if (expectAnalysisDiagnosticChange)
            {
                Assert.That(
                    analysisDiagnosticReasons,
                    Is.EqualTo(new[] { IncrementalStepRunReason.Modified })
                );
            }

            if (assertOfficialUnionSupportCaching)
            {
                Assert.That(
                    officialUnionSupportReasons,
                    Is.EqualTo(new[] { IncrementalStepRunReason.Unchanged })
                );
                Assert.That(
                    sourceGenerationModelReasons,
                    Is.EqualTo(new[]
                    {
                        expectModelChange
                            ? IncrementalStepRunReason.Modified
                            : IncrementalStepRunReason.Cached,
                    })
                );
            }

            if (expectModelChange || expectAnalysisDiagnosticChange)
            {
                Assert.That(
                    outputReasons,
                    Does.Contain(IncrementalStepRunReason.Modified)
                        .Or.Contain(IncrementalStepRunReason.New)
                );

                if (expectAnalysisDiagnosticChange)
                {
                    Assert.That(outputReasons, Does.Contain(IncrementalStepRunReason.Cached));
                }
            }
            else
            {
                Assert.That(outputReasons, Does.Contain(IncrementalStepRunReason.Cached));
                Assert.That(outputReasons, Does.Not.Contain(IncrementalStepRunReason.Modified));
            }
        });
    }

    private static IncrementalStepRunReason[] GetRunReasons(
        ImmutableDictionary<string, ImmutableArray<IncrementalGeneratorRunStep>> trackedSteps,
        string trackingName
    )
    {
        return trackedSteps[trackingName]
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();
    }
    #endregion
}
