using MemoryPack;
using Microsoft.CodeAnalysis;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class MemoryPackSerializerDiagnosticTests
{
    #region Tests
    [TestCase("MemoryPackable")]
    [TestCase("MemoryPackable(GenerateType.NoGenerate)")]
    public void MemoryPackableAttributeConflictsWithSerializerGeneration(string attribute)
    {
        AssertRejected(
            $$"""
            using MemoryPack;

            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [{{attribute}}]
            [TaggedUnionMemoryPackSerializer]
            public partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            expectedDiagnosticId: "MTUMP0002",
            expectedLocationText: attribute
        );
    }

    [Test]
    public void ExistingMemoryPackableInterfaceConflictsWithSerializerGeneration()
    {
        AssertRejected(
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionMemoryPackSerializer]
            public partial struct Foo :
                ITaggedUnionCases<int, string>,
                global::MemoryPack.IMemoryPackable<Foo>
            {
            }
            """,
            expectedDiagnosticId: "MTUMP0002",
            expectedLocationText: "global::MemoryPack.IMemoryPackable<Foo>"
        );
    }

    [Test]
    public void StaticConstructorConflictsWithSerializerGeneration()
    {
        AssertRejected(
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
            """,
            expectedDiagnosticId: "MTUMP0003",
            expectedLocationText: "Foo"
        );
    }

    #endregion

    #region Static Methods
    private static void AssertRejected(string sourceCode, string expectedDiagnosticId, string expectedLocationText)
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
        var memoryPackDiagnostics = diagnostics.Where(diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id.StartsWith("MTUMP", StringComparison.Ordinal)
        );

        Helper.AssertDiagnosticLocationText(memoryPackDiagnostics, expectedDiagnosticId, expectedLocationText);
        Assert.That(generatedCodes, Is.Empty);
    }
    #endregion
}
