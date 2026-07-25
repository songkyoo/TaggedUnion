#if NET11_0_OR_GREATER
using System.Reflection;
using Microsoft.CodeAnalysis;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class OfficialUnionTests
{
    [Test]
    public void InterfaceCaseImplicitlyConvertsToTaggedUnion()
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
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests.OfficialUnion.Generated",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        GeneratorDriver updatedDriver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics
        );

        var errorDiagnostics = outputCompilation
            .GetDiagnostics()
            .Concat(generatorDiagnostics)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        var generatedCode = updatedDriver
            .GetRunResult()
            .Results
            .Single()
            .GeneratedSources
            .Single()
            .SourceText
            .ToString();

        Assert.Multiple(() =>
        {
            Assert.That(
                errorDiagnostics,
                Is.Empty,
                string.Join(Environment.NewLine, errorDiagnostics.Select(x => x.ToString()))
            );
            Assert.That(
                generatedCode,
                Does.Contain("[global::System.Runtime.CompilerServices.Union]")
            );
            Assert.That(generatedCode, Does.Not.Contain("implicit operator"));
        });

        using var stream = new MemoryStream();
        var emitResult = outputCompilation.Emit(stream);

        Assert.That(
            emitResult.Success,
            Is.True,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(x => x.ToString()))
        );

        var assembly = Assembly.Load(stream.ToArray());
        var result = assembly
            .GetType(
                "Macaron.Union.Tests.OfficialUnion.Generated.Scenario",
                throwOnError: true
            )!
            .GetMethod("Convert", BindingFlags.Public | BindingFlags.Static)!
            .Invoke(obj: null, parameters: null);

        Assert.That(
            result!.GetType().FullName,
            Is.EqualTo("Macaron.Union.Tests.OfficialUnion.Generated.Value")
        );
    }
}
#endif
