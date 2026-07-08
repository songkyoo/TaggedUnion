using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.Union.Tests;

internal static class Helper
{
    public static void AssertGeneratedCode(string sourceCode, string[] expectedCodes)
    {
        var (_, generatedCodes) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]

        );

        Assert.That(generatedCodes, Has.Length.EqualTo(expectedCodes.Length));

        foreach (var (generatedCode, index) in generatedCodes.Select((code, index) => (code, index)))
        {
            Assert.That(
                actual: generatedCode.ReplaceLineEndings(),
                expression: Is.EqualTo(expectedCodes[index].ReplaceLineEndings())
            );
        }
    }

    public static void AssertGeneratedCode(string sourceCode, int sourceIndex, string expected)
    {
        var (_, generatedCodes) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );

        Assert.That(
            actual: generatedCodes[sourceIndex].ReplaceLineEndings(),
            expression: Is.EqualTo(expected.ReplaceLineEndings())
        );
    }

    public static void AssertGeneratedCode(string sourceCode, string expected)
    {
        AssertGeneratedCode(sourceCode, sourceIndex: 0, expected);
    }

    public static void AssertGeneratedCodeContains(string sourceCode, int sourceIndex, params string[] expectedFragments)
    {
        var (_, generatedCodes) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var generatedCode = generatedCodes[sourceIndex].ReplaceLineEndings();

        foreach (var expectedFragment in expectedFragments)
        {
            Assert.That(
                actual: generatedCode,
                expression: Does.Contain(expectedFragment.ReplaceLineEndings())
            );
        }
    }

    public static void AssertGeneratedCodeContains(string sourceCode, params string[] expectedFragments)
    {
        AssertGeneratedCodeContains(sourceCode, sourceIndex: 0, expectedFragments);
    }

    public static void AssertDiagnostic(string sourceCode, string expectedDiagnosticId)
    {
        AssertDiagnostics(sourceCode, expectedDiagnosticId);
    }

    public static void AssertDiagnostics(string sourceCode, params string[] expectedDiagnosticIds)
    {
        var (diagnostics, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var actualDiagnosticIds = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        Assert.That(actualDiagnosticIds, Is.EquivalentTo(expectedDiagnosticIds));
    }

    public static void AssertDiagnosticLocationText(
        string sourceCode,
        string expectedDiagnosticId,
        string expectedLocationText
    )
    {
        var (diagnostics, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var errorDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        var actualDiagnosticIds = errorDiagnostics
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        Assert.That(actualDiagnosticIds, Is.EquivalentTo(new[] { expectedDiagnosticId }));

        var diagnostic = errorDiagnostics.Single(diagnostic => diagnostic.Id == expectedDiagnosticId);

        Assert.That(diagnostic.Location.SourceTree, Is.Not.Null);
        Assert.That(
            actual: diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan),
            expression: Is.EqualTo(expectedLocationText)
        );
    }

    private static (ImmutableArray<Diagnostic> diagnostics, string[] generatedCodes) CompileAndGetResults<T>(
        string sourceCode,
        Assembly[]? additionalAssemblies = null
    ) where T : IIncrementalGenerator, new()
    {
        var references = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Concat(additionalAssemblies ?? [])
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create(
            assemblyName: "Macaron.TaggedUnion.Tests",
            syntaxTrees: [syntaxTree],
            references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        var generator = new T();
        var driver = CSharpGeneratorDriver.Create(generator);

        var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
        var generatedSources = result.GeneratedSources;
        var generatedCodes = generatedSources.Select(source => source.SourceText.ToString()).ToArray();

        var allDiagnostics = compilation.GetDiagnostics()
            .Concat(result.Diagnostics)
            .ToImmutableArray();

        return (allDiagnostics, generatedCodes);
    }
}
