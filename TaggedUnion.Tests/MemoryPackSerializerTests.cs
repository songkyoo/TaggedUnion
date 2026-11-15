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

        return EmitGeneratedAssembly(compilation, driver);
    }
    #endregion

    #region Fields
    private Func<byte[], ITaggedUnion> _deserialize = null!;
    private Func<ITaggedUnion, byte[]> _serialize = null!;
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
                ITaggedUnionCases<
                    int,
                    string,
                    global::Macaron.Union.Tests.MemoryPackTestValue,
                    global::Macaron.Union.Tests.ManualMemoryPackTestValue
                >
            {
                public static bool WasStaticConstructorCalled { get; private set; }

                static partial void StaticConstructor()
                {
                    WasStaticConstructorCalled = true;
                }
            }

            public static class MemoryPackUnionOperations
            {
                public static byte[] Serialize(ITaggedUnion value)
                {
                    return global::MemoryPack.MemoryPackSerializer.Serialize((MemoryPackUnion)value);
                }

                public static ITaggedUnion Deserialize(byte[] bytes)
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

        _serialize = operationsType.GetMethod("Serialize")!.CreateDelegate<Func<ITaggedUnion, byte[]>>();
        _deserialize = operationsType.GetMethod("Deserialize")!.CreateDelegate<Func<byte[], ITaggedUnion>>();
    }

    [Test]
    public void RoundTripsPackableCase()
    {
        var expected = new MemoryPackTestValue { Value = 42 };
        var actual = RoundTrip(expected);

        Assert.That(actual, Is.TypeOf<MemoryPackTestValue>());
        Assert.That(((MemoryPackTestValue)actual!).Value, Is.EqualTo(expected.Value));
    }

    [Test]
    public void RoundTripsCaseThatImplementsMemoryPackable()
    {
        var expected = new ManualMemoryPackTestValue(73);
        var actual = RoundTrip(expected);

        Assert.That(actual, Is.TypeOf<ManualMemoryPackTestValue>());
        Assert.That(((ManualMemoryPackTestValue)actual!).Value, Is.EqualTo(expected.Value));
    }

    [Test]
    public void RoundTripsIntCase()
    {
        Assert.That(RoundTrip(7), Is.EqualTo(7));
    }

    [Test]
    public void SerializesDefaultAsNullUnion()
    {
        var value = (ITaggedUnion)Activator.CreateInstance(_unionType)!;

        Assert.That(_serialize(value), Is.EqualTo(new byte[] { 255 }));
    }

    [Test]
    public void DeserializesNullUnionHeaderAsDefault()
    {
        Assert.That(_deserialize([255]).HasValue, Is.False);
    }

    [Test]
    public void CallsStaticConstructorPartialMethod()
    {
        Assert.That(
            _unionType.GetProperty("WasStaticConstructorCalled")!.GetValue(obj: null),
            Is.True
        );
    }

    [Test]
    public void UsesWideNativeUnionHeaderForTag255()
    {
        var bytes = _serialize(CreateUnion("hello"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 250, 255, 0 }));
            Assert.That(_deserialize(bytes).Value, Is.EqualTo("hello"));
        }
    }

    private ITaggedUnion CreateUnion(object value)
    {
        return (ITaggedUnion)Activator.CreateInstance(_unionType, value)!;
    }

    private object? RoundTrip(object value)
    {
        return _deserialize(_serialize(CreateUnion(value))).Value;
    }
    #endregion
}
