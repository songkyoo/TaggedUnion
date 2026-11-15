#if NET11_0_OR_GREATER
using Microsoft.CodeAnalysis;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class OfficialUnionTests
{
    [Test]
    public void RuntimeTaggedUnionSupportsUnionPatterns()
    {
        IRuntimeValue value = new RuntimeValue();
        TaggedUnion<IRuntimeValue, int> union = value;

        var result = union switch
        {
            IRuntimeValue => "value",
            int => "int",
        };

        Assert.That(result, Is.EqualTo("value"));
    }

    [Test]
    public void InterfaceCaseImplicitlyConvertsToNamedUnion()
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode:
            """
            #nullable enable

            namespace Macaron.Union.Tests.OfficialUnion.Generated;

            public interface IValue
            {
            }

            public sealed class Value : IValue
            {
            }

            [global::Macaron.Union.TaggedUnion]
            public readonly partial struct ValueOrInt
                : global::Macaron.Union.ITaggedUnionCases<IValue, int>
            {
            }

            public static class Scenario
            {
                public static object Convert()
                {
                    IValue value = new Value();
                    ValueOrInt union = value;

                    return union.Value!;
                }

                public static object ConvertBack()
                {
                    TaggedUnion<IValue, int> value = new Value();
                    ValueOrInt union = value;

                    return union.Value!;
                }
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests.OfficialUnion.Generated",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        var assembly = EmitGeneratedAssembly(compilation, driver);
        var scenario = assembly.GetType("Macaron.Union.Tests.OfficialUnion.Generated.Scenario", throwOnError: true)!;
        var expected = assembly.GetType("Macaron.Union.Tests.OfficialUnion.Generated.Value", throwOnError: true)!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(scenario.GetMethod("Convert")!.Invoke(null, null), Is.TypeOf(expected));
            Assert.That(scenario.GetMethod("ConvertBack")!.Invoke(null, null), Is.TypeOf(expected));
        }
    }

    private interface IRuntimeValue
    {
    }

    private sealed class RuntimeValue : IRuntimeValue
    {
    }
}
#endif
