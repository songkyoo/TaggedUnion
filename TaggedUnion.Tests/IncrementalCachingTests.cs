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
    private const string ModelTrackingName = "UnionGenerationModel";
    private const string OfficialUnionSupportTrackingName = "OfficialUnionSupport";
    private const string SourceGenerationModelTrackingName = "TaggedUnionSourceGenerationModel";
    #endregion

    #region Tests
    [Test]
    public void TaggedUnionGeneratorCachesSourceOutputWhenModelIsUnchanged()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
                public static int IgnoredByGenerationModel => 42;
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assertOfficialUnionSupportCaching: true
        );
    }

    [Test]
    public void TaggedUnionJsonSerializerGeneratorCachesSourceOutputWhenModelIsUnchanged()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionJsonSerializerGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionJsonSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionJsonSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
                public static int IgnoredByGenerationModel => 42;
            }
            """,
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionJsonSerializerAttribute).Assembly,
                typeof(JsonSerializer).Assembly,
            ]
        );
    }

    [Test]
    public void TaggedUnionJsonSerializerGeneratorCachesSourceOutputWhenConversionOperatorPolicyChanges()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionJsonSerializerGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionJsonSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(GenerateConversionOperators = true)]
            [TaggedUnionJsonSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionJsonSerializerAttribute).Assembly,
                typeof(JsonSerializer).Assembly,
            ]
        );
    }

    [Test]
    public void TaggedUnionMemoryPackSerializerGeneratorCachesSourceOutputWhenModelIsUnchanged()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionMemoryPackSerializerGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
                public static int IgnoredByGenerationModel => 42;
            }
            """,
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                typeof(MemoryPackSerializer).Assembly,
            ]
        );
    }

    [Test]
    public void TaggedUnionMemoryPackSerializerGeneratorCachesSourceOutputWhenConversionOperatorPolicyChanges()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionMemoryPackSerializerGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(GenerateConversionOperators = true)]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                typeof(MemoryPackSerializer).Assembly,
            ]
        );
    }

    [Test]
    public void TaggedUnionMemoryPackSerializerGeneratorUpdatesSourceOutputWhenCaseBecomesMemoryPackable()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionMemoryPackSerializerGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public partial class Case
            {
            }

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<Case, int>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [global::MemoryPack.MemoryPackable]
            public partial class Case
            {
            }

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial struct Foo : ITaggedUnionCases<Case, int>
            {
            }
            """,
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                typeof(MemoryPackSerializer).Assembly,
            ],
            expectModelChange: true
        );
    }

    [Test]
    public void TaggedUnionGeneratorUpdatesSourceOutputWhenInterfaceCaseTypeChanges()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<long, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assertOfficialUnionSupportCaching: true,
            expectModelChange: true
        );
    }

    [Test]
    public void TaggedUnionGeneratorUpdatesSourceOutputWhenStateParameterNameChanges()
    {
        AssertSourceOutputRunReasons(
            generator: new TaggedUnionGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(StateParamName = "context")]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assertOfficialUnionSupportCaching: true,
            expectModelChange: true
        );
    }
    #endregion

    #region Static Methods
    private static void AssertSourceOutputRunReasons(
        IIncrementalGenerator generator,
        string sourceCode,
        string updatedSourceCode,
        Assembly[] additionalAssemblies,
        bool assertOfficialUnionSupportCaching = false,
        bool expectModelChange = false
    )
    {
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
        GeneratorDriver trackingDriver = driver.RunGenerators(compilation);
        var originalSyntaxTree = compilation.SyntaxTrees.Single();
        var updatedSyntaxTree = CSharpSyntaxTree.ParseText(
            updatedSourceCode,
            (CSharpParseOptions)originalSyntaxTree.Options
        );
        var updatedCompilation = compilation.ReplaceSyntaxTree(originalSyntaxTree, updatedSyntaxTree);

        trackingDriver = trackingDriver.RunGenerators(updatedCompilation);

        var result = trackingDriver.GetRunResult().Results.Single();
        var analysisResultReasons = GetRunReasons(result.TrackedSteps, AnalysisResultTrackingName);
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
                        : IncrementalStepRunReason.Cached,
                })
            );

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

            if (expectModelChange)
            {
                Assert.That(
                    outputReasons,
                    Does.Contain(IncrementalStepRunReason.Modified)
                        .Or.Contain(IncrementalStepRunReason.New)
                );
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
