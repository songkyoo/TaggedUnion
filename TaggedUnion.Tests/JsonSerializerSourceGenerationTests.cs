using System.Text.Json;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class JsonSerializerSourceGenerationTests
{
    [Test]
    public void GeneratesArrayConverterUsingInternalValueProperties()
    {
        var (_, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionJsonSerializerGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(string), tag: 42)]
            [TaggedUnionJsonSerializer]
            public partial struct Foo : ITaggedUnion<int, string>
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
        var generatedCode = generatedCodes.Single().ReplaceLineEndings();
        var expectedFragments = new[]
        {
            "[global::System.Text.Json.Serialization.JsonConverter(typeof(Foo.JsonConverter))]",
            "readonly partial struct Foo",
            "private sealed class JsonConverter : global::System.Text.Json.Serialization.JsonConverter<Foo>",
            "public override bool HandleNull => true;",
            "if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)",
            "return default;",
            "1 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<int>(ref reader, options))",
            "42 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options)",
            "?? throw new global::System.Text.Json.JsonException(\"The value for tag 42 cannot be null.\")",
            "if (value._tag == 0)",
            "writer.WriteNullValue();",
            "writer.WriteStartArray();",
            "writer.WriteNumberValue(value._tag);",
            "global::System.Text.Json.JsonSerializer.Serialize<int>(writer, value.__1, options);",
            "global::System.Text.Json.JsonSerializer.Serialize<string>(writer, value.__42, options);",
        };

        foreach (var expectedFragment in expectedFragments)
        {
            Assert.That(
                actual: generatedCode,
                expression: Does.Contain(expectedFragment.ReplaceLineEndings())
            );
        }

        Assert.That(generatedCode, Does.Not.Contain("value._reference"));
        Assert.That(generatedCode, Does.Not.Contain("value._unmanaged"));
        Assert.That(generatedCode, Does.Not.Contain("value._value"));
    }
}
