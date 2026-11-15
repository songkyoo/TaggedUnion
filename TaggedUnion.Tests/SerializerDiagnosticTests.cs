using System.Reflection;
using System.Text.Json;
using MemoryPack;
using Microsoft.CodeAnalysis;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture(
    typeof(TaggedUnionJsonSerializerGenerator),
    "TaggedUnionJsonSerializer",
    "MTUJS0001",
    typeof(TaggedUnionJsonSerializerAttribute),
    typeof(JsonSerializer)
)]
[TestFixture(
    typeof(TaggedUnionMemoryPackSerializerGenerator),
    "TaggedUnionMemoryPackSerializer",
    "MTUMP0001",
    typeof(TaggedUnionMemoryPackSerializerAttribute),
    typeof(MemoryPackSerializer)
)]
public sealed class SerializerDiagnosticTests<TGenerator>(
    string attribute,
    string missingTaggedUnionDiagnosticId,
    Type attributeType,
    Type serializerType
) where TGenerator : IIncrementalGenerator, new()
{
    private readonly Assembly[] _references =
        [typeof(TaggedUnionAttribute).Assembly, attributeType.Assembly, serializerType.Assembly];

    [Test]
    public void SerializerAttributeRequiresTaggedUnion()
    {
        var (diagnostics, generatedCodes, _, _) = CompileAndGetResults<TGenerator>(
            $$"""
            namespace Macaron.Union.Tests;

            [{{attribute}}]
            public partial struct Foo { }
            """,
            _references
        );

        AssertDiagnosticLocationText(
            diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            missingTaggedUnionDiagnosticId,
            attribute
        );
        Assert.That(generatedCodes, Is.Empty);
    }

    [TestCase("record struct", " : ITaggedUnionCases<int, string>", TestName = "SerializerSkipsRecordStruct")]
    [TestCase("struct", "", TestName = "SerializerSkipsMissingCases")]
    [TestCase("struct", " : ITaggedUnionCases<State, string>", TestName = "SerializerSkipsStateParameterConflict")]
    public void InvalidUnionSilentlySkipsSerializerGeneration(string declaration, string cases)
    {
        var (diagnostics, generatedCodes, _, _) = CompileAndGetResults<TGenerator>(
            $$"""
            namespace Macaron.Union.Tests;

            public sealed class State { }

            [TaggedUnion]
            [{{attribute}}]
            public readonly partial {{declaration}} Foo{{cases}} { }
            """,
            _references
        );

        AssertNoErrors(diagnostics);
        Assert.That(generatedCodes, Is.Empty);
    }
}
