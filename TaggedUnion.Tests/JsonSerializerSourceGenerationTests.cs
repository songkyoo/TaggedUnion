using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class JsonSerializerSourceGenerationTests
{
    [Test]
    public void GeneratesArrayConverterUsingUnionStorage()
    {
        AssertJsonSerializerGeneratedCodeContains(
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
            expectedFragments:
            [
                "[global::System.Text.Json.Serialization.JsonConverter(typeof(Foo.JsonConverter))]",
                "private sealed class JsonConverter : global::System.Text.Json.Serialization.JsonConverter<Foo>",
                "1 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<int>(ref reader, options))",
                "42 => new Foo(global::System.Text.Json.JsonSerializer.Deserialize<string>(ref reader, options)",
                "writer.WriteStartArray();",
                "writer.WriteNumberValue(value._tag);",
                "global::System.Text.Json.JsonSerializer.Serialize<int>(writer, value._unmanaged.Value1, options);",
                "global::System.Text.Json.JsonSerializer.Serialize<string>(writer, (string)value._reference!, options);",
            ]
        );
    }
}
