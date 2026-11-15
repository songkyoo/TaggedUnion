using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class JsonSerializerTests
{
    #region Static Methods
    private static Assembly CompileJsonSerializableAssembly(string sourceCode)
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode,
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionJsonSerializerAttribute).Assembly,
                typeof(JsonSerializer).Assembly,
            ],
            assemblyName: "Macaron.TaggedUnion.JsonSerializer.Tests.Generated",
            generators:
            [
                new TaggedUnionGenerator().AsSourceGenerator(),
                new TaggedUnionJsonSerializerGenerator().AsSourceGenerator(),
            ]
        );

        return EmitGeneratedAssembly(compilation, driver);
    }
    #endregion

    #region Fields
    private Type _unionType = null!;
    #endregion

    #region Methods
    [OneTimeSetUp]
    public void SetUp()
    {
        var assembly = CompileJsonSerializableAssembly(
            sourceCode:
            """
            namespace Macaron.Union.JsonSerializer.Tests.Generated;

            [TaggedUnion]
            [TaggedUnionCase(typeof(string), tag: 42)]
            [TaggedUnionJsonSerializer]
            public readonly partial struct JsonUnion : ITaggedUnionCases<int, string>
            {
            }
            """
        );

        _unionType = assembly.GetType(
            name: "Macaron.Union.JsonSerializer.Tests.Generated.JsonUnion",
            throwOnError: true
        )!;
    }

    [TestCase(7, "[1,7]")]
    [TestCase("hello", """[42,"hello"]""")]
    public void WriteProducesTagAndValueArray(object value, string expected)
    {
        var union = Activator.CreateInstance(_unionType, value);

        Assert.That(JsonSerializer.Serialize(union, _unionType), Is.EqualTo(expected));
    }

    [TestCase("[1,7]", 7)]
    [TestCase("""[42,"hello"]""", "hello")]
    public void ReadUsesTagToSelectCase(string json, object expected)
    {
        var union = (ITaggedUnion)JsonSerializer.Deserialize(json, _unionType)!;

        Assert.That(union.Value, Is.EqualTo(expected));
    }

    [Test]
    public void ReadNullProducesUninitializedValue()
    {
        var union = (ITaggedUnion)JsonSerializer.Deserialize("null", _unionType)!;

        Assert.That(union.HasValue, Is.False);
    }

    [TestCase("{}")]
    [TestCase("[\"1\",7]")]
    [TestCase("[0,null]")]
    [TestCase("[99,null]")]
    [TestCase("[1]")]
    [TestCase("[1,7,8]")]
    [TestCase("[42,null]")]
    public void ReadRejectsInvalidPayload(string json)
    {
        Assert.That(
            () => JsonSerializer.Deserialize(json, _unionType),
            Throws.TypeOf<JsonException>()
        );
    }

    [Test]
    public void WriteUninitializedValueAsNull()
    {
        var value = Activator.CreateInstance(_unionType)!;

        Assert.That(JsonSerializer.Serialize(value, _unionType), Is.EqualTo("null"));
    }
    #endregion
}
