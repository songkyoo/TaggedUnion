using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class IncrementalCachingTests
{
    #region Constants
    private const string ModelTrackingName = "UnionGenerationModel";
    #endregion

    #region Tests
    [Test]
    public void TaggedUnionGeneratorCachesSourceOutputWhenModelIsUnchanged()
    {
        AssertSourceOutputIsCached(
            generator: new TaggedUnionGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(int), typeof(string))]
            public readonly partial struct Foo
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(int), typeof(string))]
            public readonly partial struct Foo
            {
                // This edit does not change the generation model.
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
    }

    [Test]
    public void TaggedUnionJsonSerializerGeneratorCachesSourceOutputWhenModelIsUnchanged()
    {
        AssertSourceOutputIsCached(
            generator: new TaggedUnionJsonSerializerGenerator(),
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(int), typeof(string))]
            [TaggedUnionJsonSerializer]
            public readonly partial struct Foo
            {
            }
            """,
            updatedSourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(int), typeof(string))]
            [TaggedUnionJsonSerializer]
            public readonly partial struct Foo
            {
                // This edit does not change the generation model.
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
    #endregion

    #region Static Methods
    private static void AssertSourceOutputIsCached(
        IIncrementalGenerator generator,
        string sourceCode,
        string updatedSourceCode,
        Assembly[] additionalAssemblies
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
        var modelReasons = GetRunReasons(result.TrackedSteps, ModelTrackingName);
        var outputReasons = result
            .TrackedOutputSteps
            .Values
            .SelectMany(static steps => steps)
            .SelectMany(static step => step.Outputs)
            .Select(static output => output.Reason)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(modelReasons, Is.EqualTo(new[] { IncrementalStepRunReason.Unchanged }));
            Assert.That(outputReasons, Does.Contain(IncrementalStepRunReason.Cached));
            Assert.That(outputReasons, Does.Not.Contain(IncrementalStepRunReason.Modified));
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
