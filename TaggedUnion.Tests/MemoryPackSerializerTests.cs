using System.Reflection;
using MemoryPack;
using Microsoft.CodeAnalysis;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class MemoryPackSerializerTests
{
    #region Static Methods
    private static Assembly CompileMemoryPackSerializableAssembly(string sourceCode)
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode,
            additionalAssemblies:
            [
                typeof(TaggedUnionAttribute).Assembly,
                typeof(TaggedUnionMemoryPackSerializerAttribute).Assembly,
                typeof(MemoryPackSerializer).Assembly,
                typeof(MemoryPackTestValue).Assembly,
            ],
            assemblyName: "Macaron.TaggedUnion.MemoryPackSerializer.Tests.Generated",
            generators:
            [
                new TaggedUnionGenerator().AsSourceGenerator(),
                new TaggedUnionMemoryPackSerializerGenerator().AsSourceGenerator(),
            ],
            preprocessorSymbols: ["NET7_0_OR_GREATER"]
        );

        driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics
        );

        var errorDiagnostics = outputCompilation
            .GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(
            errorDiagnostics,
            Is.Empty,
            string.Join(Environment.NewLine, errorDiagnostics.Select(diagnostic => diagnostic.ToString()))
        );

        using var stream = new MemoryStream();
        var emitResult = outputCompilation.Emit(stream);

        Assert.That(
            emitResult.Success,
            Is.True,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(diagnostic => diagnostic.ToString()))
        );

        return Assembly.Load(stream.ToArray());
    }
    #endregion

    #region Fields
    private MethodInfo _deserializeMethod = null!;
    private MethodInfo _serializeMethod = null!;
    private Type _unionType = null!;
    #endregion

    #region Methods
    [OneTimeSetUp]
    public void SetUp()
    {
        var assembly = CompileMemoryPackSerializableAssembly(
            sourceCode:
            """
            namespace Macaron.Union.MemoryPackSerializer.Tests.Generated;

            [TaggedUnion]
            [TaggedUnionCase(typeof(string), tag: 255)]
            [TaggedUnionMemoryPackSerializer]
            public partial struct MemoryPackUnion :
                ITaggedUnionCases<int, string, global::Macaron.Union.Tests.MemoryPackTestValue>
            {
            }

            public static class MemoryPackUnionOperations
            {
                public static byte[] Serialize(MemoryPackUnion value)
                {
                    return global::MemoryPack.MemoryPackSerializer.Serialize<MemoryPackUnion>(value);
                }

                public static MemoryPackUnion Deserialize(byte[] bytes)
                {
                    return global::MemoryPack.MemoryPackSerializer.Deserialize<MemoryPackUnion>(bytes);
                }
            }
            """
        );

        _unionType = assembly.GetType(
            name: "Macaron.Union.MemoryPackSerializer.Tests.Generated.MemoryPackUnion",
            throwOnError: true
        )!;
        var operationsType = assembly.GetType(
            name: "Macaron.Union.MemoryPackSerializer.Tests.Generated.MemoryPackUnionOperations",
            throwOnError: true
        )!;

        _serializeMethod = operationsType.GetMethod("Serialize")!;
        _deserializeMethod = operationsType.GetMethod("Deserialize")!;
    }

    [Test]
    public void RoundTripsPackableCase()
    {
        var expected = new MemoryPackTestValue { Value = 42 };
        var actual = RoundTrip(expected);

        Assert.That(actual, Is.TypeOf<MemoryPackTestValue>());
        Assert.That(((MemoryPackTestValue)actual!).Value, Is.EqualTo(expected.Value));
    }

    [TestCase(7)]
    [TestCase("hello")]
    public void RoundTripsValueCases(object expected)
    {
        Assert.That(RoundTrip(expected), Is.EqualTo(expected));
    }

    [Test]
    public void SerializesDefaultAsNullUnion()
    {
        var value = Activator.CreateInstance(_unionType)!;
        var bytes = Serialize(value);
        var roundTripped = Deserialize(bytes);

        Assert.Multiple(() =>
        {
            Assert.That(bytes, Is.EqualTo(new byte[] { 255 }));
            Assert.That(_unionType.GetProperty("HasValue")!.GetValue(roundTripped), Is.False);
        });
    }

    [Test]
    public void UsesWideNativeUnionHeaderForTag255()
    {
        var value = CreateUnion("hello");
        var bytes = Serialize(value);

        Assert.Multiple(() =>
        {
            Assert.That(bytes[0], Is.EqualTo(250));
            Assert.That(bytes[1], Is.EqualTo(255));
            Assert.That(bytes[2], Is.EqualTo(0));
            Assert.That(
                _unionType.GetProperty("Value")!.GetValue(Deserialize(bytes)),
                Is.EqualTo("hello")
            );
        });
    }

    private object CreateUnion(object value)
    {
        var constructor = _unionType.GetConstructor([value.GetType()])!;

        return constructor.Invoke([value]);
    }

    private object Deserialize(byte[] bytes)
    {
        return _deserializeMethod.Invoke(obj: null, parameters: [bytes])!;
    }

    private object? RoundTrip(object value)
    {
        var union = CreateUnion(value);
        var bytes = Serialize(union);
        var roundTripped = Deserialize(bytes);

        return _unionType.GetProperty("Value")!.GetValue(roundTripped);
    }

    private byte[] Serialize(object value)
    {
        return (byte[])_serializeMethod.Invoke(obj: null, parameters: [value])!;
    }
    #endregion
}
