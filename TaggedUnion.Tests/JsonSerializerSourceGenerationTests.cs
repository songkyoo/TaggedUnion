using System.Text.Json;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class JsonSerializerSourceGenerationTests
{
    private string _generatedCode = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        var (_, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionJsonSerializerGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(string), tag: 42)]
            [TaggedUnionJsonSerializer]
            public partial struct Foo : ITaggedUnionCases<int, string>
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
        _generatedCode = generatedCodes.Single();
    }

    [Test]
    public void AttachesTheConverterToTheUnion()
    {
        AssertContainsFragments(
            _generatedCode,
            "[global::System.Text.Json.Serialization.JsonConverter(typeof(Foo.JsonConverter))]",
            "readonly partial struct Foo",
            "private sealed class JsonConverter : global::System.Text.Json.Serialization.JsonConverter<Foo>",
            "public override bool HandleNull => true;"
        );
    }

    [Test]
    public void ReadsTheTypedCaseForEachTag()
    {
        AssertContainsFragments(
            _generatedCode,
            "1 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<int>(ref reader, options))",
            "42 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options)",
            "?? throw new global::System.Text.Json.JsonException(\"The value for tag 42 cannot be null.\")"
        );
    }

    [Test]
    public void WritesThroughTypedCaseProperties()
    {
        AssertContainsFragments(
            _generatedCode,
            "writer.WriteNumberValue(value._tag);",
            "global::System.Text.Json.JsonSerializer.Serialize<int>(writer, value.__1, options);",
            "global::System.Text.Json.JsonSerializer.Serialize<string>(writer, value.__42, options);"
        );
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_generatedCode, Does.Not.Contain("value._reference"));
            Assert.That(_generatedCode, Does.Not.Contain("value._unmanaged"));
            Assert.That(_generatedCode, Does.Not.Contain("value._value"));
        }
    }
}
