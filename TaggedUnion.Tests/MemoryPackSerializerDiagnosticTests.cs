using MemoryPack;
using Microsoft.CodeAnalysis;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class MemoryPackSerializerDiagnosticTests
{
    #region Tests
    [Test]
    public void MemoryPackSerializerAttributeWithoutTaggedUnionAttribute()
    {
        var (diagnostics, generatedCodes) = Compile(
            """
            namespace Macaron.Union.Tests;

            [TaggedUnionMemoryPackSerializer]
            public partial struct Foo
            {
            }
            """
        );

        AssertDiagnostic(
            diagnostics,
            expectedDiagnosticId: "MTUMP0001",
            expectedLocationText: "TaggedUnionMemoryPackSerializer"
        );
        Assert.That(generatedCodes, Is.Empty);
    }

    [Test]
    public void MemoryPackableAttributeConflictsWithSerializerGeneration()
    {
        var (diagnostics, generatedCodes) = Compile(
            """
            using MemoryPack;

            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [MemoryPackable]
            [TaggedUnionMemoryPackSerializer]
            public partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """
        );

        AssertDiagnostic(
            diagnostics,
            expectedDiagnosticId: "MTUMP0002",
            expectedLocationText: "MemoryPackable"
        );
        Assert.That(generatedCodes, Is.Empty);
    }

    [Test]
    public void ExistingMemoryPackableInterfaceConflictsWithSerializerGeneration()
    {
        var (diagnostics, generatedCodes) = Compile(
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public partial struct Foo :
                ITaggedUnionCases<int, string>,
                global::MemoryPack.IMemoryPackable<Foo>
            {
            }
            """
        );

        AssertDiagnostic(
            diagnostics,
            expectedDiagnosticId: "MTUMP0002",
            expectedLocationText: "global::MemoryPack.IMemoryPackable<Foo>"
        );
        Assert.That(generatedCodes, Is.Empty);
    }

    [Test]
    public void StaticConstructorConflictsWithSerializerGeneration()
    {
        var (diagnostics, generatedCodes) = Compile(
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public partial struct Foo : ITaggedUnionCases<int, string>
            {
                static Foo()
                {
                }
            }
            """
        );

        AssertDiagnostic(
            diagnostics,
            expectedDiagnosticId: "MTUMP0003",
            expectedLocationText: "Foo"
        );
        Assert.That(generatedCodes, Is.Empty);
    }

    [Test]
    public void MemoryPackableNoGenerateIsAllowed()
    {
        var (diagnostics, generatedCodes) = Compile(
            """
            using MemoryPack;

            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [MemoryPackable(GenerateType.NoGenerate)]
            [TaggedUnionMemoryPackSerializer]
            public partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """
        );

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(generatedCodes, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void MemoryPackSerializerGeneratorDoesNotReportTaggedUnionDiagnostics()
    {
        var (diagnostics, generatedCodes) = Compile(
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public readonly partial record struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """
        );

        Assert.Multiple(() =>
        {
            Assert.That(diagnostics, Is.Empty);
            Assert.That(generatedCodes, Is.Empty);
        });
    }
    #endregion

    #region Static Methods
    private static void AssertDiagnostic(
        Diagnostic[] diagnostics,
        string expectedDiagnosticId,
        string expectedLocationText
    )
    {
        Assert.That(
            diagnostics.Select(diagnostic => diagnostic.Id),
            Is.EqualTo(new[] { expectedDiagnosticId })
        );

        var diagnostic = diagnostics.Single();

        Assert.That(diagnostic.Location.SourceTree, Is.Not.Null);
        Assert.That(
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan),
            Is.EqualTo(expectedLocationText)
        );
    }

    private static (Diagnostic[] diagnostics, string[] generatedCodes) Compile(string sourceCode)
    {
        var (diagnostics, generatedCodes, _, _) =
            Helper.CompileAndGetResults<TaggedUnionMemoryPackSerializerGenerator>(
                sourceCode,
                additionalAssemblies:
                [
                    typeof(TaggedUnionAttribute).Assembly,
                    typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                    typeof(MemoryPackableAttribute).Assembly,
                ]
            );
        var memoryPackDiagnostics = diagnostics
            .Where(diagnostic =>
                diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Id.StartsWith("MTUMP", StringComparison.Ordinal)
            )
            .ToArray();

        return (memoryPackDiagnostics, generatedCodes);
    }
    #endregion
}
