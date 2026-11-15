using MemoryPack;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class MemoryPackSerializerSourceGenerationTests
{
    [Test]
    public void SelectsPackableMethodsForCaseThatImplementsMemoryPackable()
    {
        var (_, generatedCodes, _, _) =
            CompileAndGetResults<TaggedUnionMemoryPackSerializerGenerator>(
                sourceCode:
                """
                namespace Macaron.Union.Tests;

                [TaggedUnion]
                [TaggedUnionMemoryPackSerializer]
                public partial struct Foo :
                    ITaggedUnionCases<ManualMemoryPackTestValue, int>
                {
                }
                """,
                additionalAssemblies:
                [
                    typeof(TaggedUnionAttribute).Assembly,
                    typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                    typeof(MemoryPackableAttribute).Assembly,
                    typeof(ManualMemoryPackTestValue).Assembly,
                ]
            );
        var generatedCode = generatedCodes.Single().ReplaceLineEndings();

        Assert.Multiple(() =>
        {
            Assert.That(
                generatedCode,
                Does.Contain("case 1: writer.WritePackable(value.__1); break;")
            );
            Assert.That(
                generatedCode,
                Does.Contain(
                    "case 1: value = new Foo(reader.ReadPackable<global::Macaron.Union.Tests.ManualMemoryPackTestValue>()); break;"
                )
            );
            Assert.That(
                generatedCode,
                Does.Contain("case 2: writer.WriteValue(value.__2); break;")
            );
            Assert.That(
                generatedCode,
                Does.Contain("case 2: value = new Foo(reader.ReadValue<int>()); break;")
            );
        });
    }

    [Test]
    public void GeneratesNativeUnionFormatterAndSelectsCaseSerializationMethod()
    {
        var (_, generatedCodes, generatedHintNames, _) =
            CompileAndGetResults<TaggedUnionMemoryPackSerializerGenerator>(
                sourceCode:
                """
                namespace Macaron.Union.Tests;

                [global::MemoryPack.MemoryPackable]
                public partial class PackableCase
                {
                    public int Value { get; set; }
                }

                [global::MemoryPack.MemoryPackable(global::MemoryPack.GenerateType.NoGenerate)]
                public partial class NoGenerateCase
                {
                }

                [TaggedUnion]
                [TaggedUnionMemoryPackSerializer]
                public partial struct Foo :
                    ITaggedUnionCases<PackableCase, int, NoGenerateCase>
                {
                }
                """,
                additionalAssemblies:
                [
                    typeof(TaggedUnionAttribute).Assembly,
                    typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                    typeof(MemoryPackableAttribute).Assembly,
                ]
            );
        var generatedCode = generatedCodes.Single().ReplaceLineEndings();
        var expectedFragments = new[]
        {
            "readonly partial struct Foo : global::MemoryPack.IMemoryPackable<Foo>",
            "static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter()",
            "public static void RegisterFormatter()",
            "global::MemoryPack.MemoryPackFormatterProvider.Register<Foo>();",
            "global::MemoryPack.MemoryPackFormatterProvider.Register(new MemoryPackFormatter());",
            "writer.WriteNullUnionHeader();",
            "writer.WriteUnionHeader(value._tag);",
            "case 1: writer.WritePackable(value.__1); break;",
            "case 2: writer.WriteValue(value.__2); break;",
            "case 3: writer.WriteValue(value.__3); break;",
            "if (!reader.TryReadUnionHeader(out var tag))",
            "case 1: value = new Foo(reader.ReadPackable<global::Macaron.Union.Tests.PackableCase>()",
            "case 2: value = new Foo(reader.ReadValue<int>()); break;",
            "case 3: value = new Foo(reader.ReadValue<global::Macaron.Union.Tests.NoGenerateCase>()",
            "global::MemoryPack.MemoryPackSerializationException.ThrowInvalidTag(tag, typeof(Foo));",
            "private sealed class MemoryPackFormatter : global::MemoryPack.MemoryPackFormatter<Foo>",
        };

        Assert.Multiple(() =>
        {
            Assert.That(
                generatedHintNames,
                Is.EqualTo(new[] { "Foo_0.ba8c0e3e.MemoryPackSerializer.g.cs" })
            );

            foreach (var expectedFragment in expectedFragments)
            {
                Assert.That(
                    actual: generatedCode,
                    expression: Does.Contain(expectedFragment.ReplaceLineEndings())
                );
            }
        });
    }
}
