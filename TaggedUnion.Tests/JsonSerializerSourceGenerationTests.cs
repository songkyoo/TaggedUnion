using System.Text.Json;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class JsonSerializerSourceGenerationTests
{
    [Test]
    public void GeneratesArrayConverterUsingUnionStorage()
    {
        var (_, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionJsonSerializerGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(int), typeof(string))]
            [TaggedUnionCase(typeof(string), tag: 42)]
            [TaggedUnionJsonSerializer]
            public readonly partial struct Foo
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
            "private sealed class JsonConverter : global::System.Text.Json.Serialization.JsonConverter<Foo>",
            "public override bool HandleNull => true;",
            "if (reader.TokenType == global::System.Text.Json.JsonTokenType.Null)",
            "return default;",
            "1 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<int>(ref reader, options))",
            "42 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options)",
            "if (value._tag == 0)",
            "writer.WriteNullValue();",
            "writer.WriteStartArray();",
            "writer.WriteNumberValue(value._tag);",
            "global::System.Text.Json.JsonSerializer.Serialize<int>(writer, value._unmanaged.Value1, options);",
            "global::System.Text.Json.JsonSerializer.Serialize<string>(writer, (string)value._reference!, options);",
        };

        foreach (var expectedFragment in expectedFragments)
        {
            Assert.That(
                actual: generatedCode,
                expression: Does.Contain(expectedFragment.ReplaceLineEndings())
            );
        }
    }
}
