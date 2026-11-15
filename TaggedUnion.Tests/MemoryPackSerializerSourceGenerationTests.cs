using MemoryPack;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class MemoryPackSerializerSourceGenerationTests
{
    private string _generatedCode = null!;
    private string[] _hintNames = null!;

    [OneTimeSetUp]
    public void SetUp()
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
                    ITaggedUnionCases<PackableCase, int, NoGenerateCase, ManualMemoryPackTestValue>
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
        _generatedCode = generatedCodes.Single();
        _hintNames = generatedHintNames;
    }

    [Test]
    public void HintNameIsStable()
    {
        Assert.That(_hintNames, Is.EqualTo(new[] { "Foo_0.ba8c0e3e.MemoryPackSerializer.g.cs" }));
    }

    [Test]
    public void RegistersFormattersForBothTargetFrameworkPaths()
    {
        AssertContainsFragments(
            _generatedCode,
            "readonly partial struct Foo : global::MemoryPack.IMemoryPackable<Foo>",
            "static void global::MemoryPack.IMemoryPackFormatterRegister.RegisterFormatter()",
            "public static void RegisterFormatter()",
            "global::MemoryPack.MemoryPackFormatterProvider.Register<Foo>();",
            "global::MemoryPack.MemoryPackFormatterProvider.Register(new MemoryPackFormatter());",
            "private sealed class MemoryPackFormatter : global::MemoryPack.MemoryPackFormatter<Foo>"
        );
    }

    [Test]
    public void UsesNativeUnionHeaders()
    {
        AssertContainsFragments(
            _generatedCode,
            "writer.WriteNullUnionHeader();",
            "writer.WriteUnionHeader(value._tag);",
            "if (!reader.TryReadUnionHeader(out var tag))",
            "global::MemoryPack.MemoryPackSerializationException.ThrowInvalidTag(tag, typeof(Foo));"
        );
    }

    [TestCase(1, "Packable", "global::Macaron.Union.Tests.PackableCase")]
    [TestCase(2, "Value", "int")]
    [TestCase(3, "Value", "global::Macaron.Union.Tests.NoGenerateCase")]
    [TestCase(4, "Packable", "global::Macaron.Union.Tests.ManualMemoryPackTestValue")]
    public void SelectsReadAndWriteMethodsForEachCase(int tag, string methodSuffix, string caseType)
    {
        AssertContainsFragments(
            _generatedCode,
            $"case {tag}: writer.Write{methodSuffix}(value.__{tag}); break;",
            $"case {tag}: value = new Foo(reader.Read{methodSuffix}<{caseType}>()"
        );
    }
}
