using System.Reflection;
using Microsoft.CodeAnalysis;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class GeneratedUnionRuntimeTests
{
    private Type _unionType = null!;
    private Func<ITaggedUnion, TaggedUnion<int, string>> _toRuntime = null!;
    private Func<TaggedUnion<int, string>, ITaggedUnion> _toNamed = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode:
            """
            namespace Macaron.Union.Tests.GeneratedConversion;

            [TaggedUnion]
            [TaggedUnionCase(typeof(int), tag: 42)]
            [TaggedUnionCase(typeof(string), tag: 7)]
            public partial struct Foo : ITaggedUnionCases<int, string>
            {
                public byte TagForTest => _tag;

                public static TaggedUnion<int, string> ToRuntime(ITaggedUnion value) => (Foo)value;

                public static ITaggedUnion ToNamed(TaggedUnion<int, string> value)
                {
                    Foo union = value;
                    return union;
                }
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests.GeneratedConversion",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        _unionType = EmitGeneratedAssembly(compilation, driver)
            .GetType("Macaron.Union.Tests.GeneratedConversion.Foo", throwOnError: true)!;
        _toRuntime = _unionType.GetMethod("ToRuntime")!
            .CreateDelegate<Func<ITaggedUnion, TaggedUnion<int, string>>>();
        _toNamed = _unionType.GetMethod("ToNamed")!
            .CreateDelegate<Func<TaggedUnion<int, string>, ITaggedUnion>>();
    }

    [TestCase(42)]
    [TestCase("value")]
    [TestCase(null)]
    public void NamedUnionConvertsToRuntimeUnion(object? expected)
    {
        var namedUnion = (ITaggedUnion)(expected is null
            ? Activator.CreateInstance(_unionType)!
            : Activator.CreateInstance(_unionType, expected)!);
        var runtimeUnion = _toRuntime(namedUnion);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(runtimeUnion.HasValue, Is.EqualTo(expected is not null));
            Assert.That(runtimeUnion.Value, Is.EqualTo(expected));
        }
    }

    [TestCaseSource(nameof(RuntimeCases))]
    public void RuntimeUnionConvertsToNamedUnion(TaggedUnion<int, string> value, byte expectedTag)
    {
        var namedUnion = _toNamed(value);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(namedUnion.HasValue, Is.EqualTo(value.HasValue));
            Assert.That(namedUnion.Value, Is.EqualTo(value.Value));
            Assert.That(_unionType.GetProperty("TagForTest")!.GetValue(namedUnion), Is.EqualTo(expectedTag));
        }
    }

    [TestCase(42)]
    [TestCase("value")]
    public void NamedUnionGetReturnsTheActiveCase(object expected)
    {
        var union = Activator.CreateInstance(_unionType, expected);
        var get = _unionType.GetMethod("Get")!.MakeGenericMethod(expected.GetType());

        Assert.That(get.Invoke(union, null), Is.EqualTo(expected));
    }

    [Test]
    public void UnmanagedCasesWorkInsideAGenericContainingType()
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public partial class Holder<T>
            {
                public partial class Inner
                {
                    [TaggedUnion]
                    [TaggedUnionCase(typeof(int), tag: 42)]
                    public partial struct Foo : ITaggedUnionCases<int, long>
                    {
                    }
                }
            }

            public static class Scenario
            {
                public static (int, long) ReadValues()
                {
                    var first = new Holder<string>.Inner.Foo(42);
                    var second = new Holder<string>.Inner.Foo(5_000_000_000L);

                    return (first.Get<int>(), second.Get<long>());
                }
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests.GenericContainingType",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        var readValues = EmitGeneratedAssembly(compilation, driver)
            .GetType("Macaron.Union.Tests.Scenario", throwOnError: true)!
            .GetMethod("ReadValues")!
            .CreateDelegate<Func<(int, long)>>();

        Assert.That(readValues(), Is.EqualTo((42, 5_000_000_000L)));
    }

    [Test]
    public void GeneratedGetUsesTheDeclaredCaseTypeAndCustomTag()
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode:
            """
            using System;
            using System.Collections.Generic;

            namespace Macaron.Union.Tests;

            public partial class Holder<TValue>
            {
                [TaggedUnion]
                [TaggedUnionCase(typeof(IComparable), tag: 42)]
                public partial struct Foo : ITaggedUnionCases<IComparable, List<TValue>>
                {
                }
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests.GeneratedGet",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        var unionType = EmitGeneratedAssembly(compilation, driver)
            .GetType("Macaron.Union.Tests.Holder`1+Foo", throwOnError: true)!
            .MakeGenericType(typeof(string));
        var get = unionType.GetMethod("Get")!;
        const string text = "value";
        var list = new List<string> { text };
        var interfaceCase = Activator.CreateInstance(unionType, text);
        var listCase = Activator.CreateInstance(unionType, list);
        var uninitialized = Activator.CreateInstance(unionType);
        var getInterface = get.MakeGenericMethod(typeof(IComparable));
        var getString = get.MakeGenericMethod(typeof(string));

        Assert.Multiple(() =>
        {
            Assert.That(getInterface.Invoke(interfaceCase, null), Is.SameAs(text));
            Assert.That(get.MakeGenericMethod(typeof(List<string>)).Invoke(listCase, null), Is.SameAs(list));
            Assert.That(
                () => getString.Invoke(interfaceCase, null),
                Throws.TypeOf<TargetInvocationException>().With.InnerException.TypeOf<InvalidCastException>()
            );
            Assert.That(
                () => getInterface.Invoke(uninitialized, null),
                Throws.TypeOf<TargetInvocationException>().With.InnerException.TypeOf<InvalidCastException>()
            );
        });
    }

    private static IEnumerable<TestCaseData> RuntimeCases()
    {
        yield return new TestCaseData(new TaggedUnion<int, string>(42), (byte)42);
        yield return new TestCaseData(new TaggedUnion<int, string>("value"), (byte)7);
        yield return new TestCaseData(default(TaggedUnion<int, string>), (byte)0);
    }
}
