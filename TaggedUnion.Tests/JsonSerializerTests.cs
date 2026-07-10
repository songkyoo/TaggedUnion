using System.Text.Json;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class JsonSerializerTests
{
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

            [TaggedUnion(typeof(int), typeof(string))]
            [TaggedUnionCase(typeof(string), tag: 42)]
            [TaggedUnionJsonSerializer]
            public readonly partial struct JsonUnion
            {
            }
            """
        );

        _unionType = assembly.GetType(
            name: "Macaron.Union.JsonSerializer.Tests.Generated.JsonUnion",
            throwOnError: true
        )!;
    }

    [Test]
    public void WriteProducesTagAndValueArray()
    {
        var intValue = CreateUnion(7);
        var stringValue = CreateUnion("hello");

        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(intValue, _unionType), Is.EqualTo("[1,7]"));
            Assert.That(JsonSerializer.Serialize(stringValue, _unionType), Is.EqualTo("[42,\"hello\"]"));
        });

        #region Local Functions
        object CreateUnion(object value)
        {
            var constructor = _unionType.GetConstructor([value.GetType()])!;

            return constructor.Invoke([value]);
        }
        #endregion
    }

    [TestCase("[1,7]", 7)]
    [TestCase("[42,\"hello\"]", "hello")]
    public void ReadUsesTagToSelectCase(string json, object expected)
    {
        var value = JsonSerializer.Deserialize(json, _unionType);
        var actual = _unionType.GetProperty("Value")!.GetValue(value);

        Assert.That(actual, Is.EqualTo(expected));
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
    public void WriteRejectsUninitializedValue()
    {
        var value = Activator.CreateInstance(_unionType)!;

        Assert.That(
            () => JsonSerializer.Serialize(value, _unionType),
            Throws.TypeOf<JsonException>()
        );
    }
    #endregion
}
