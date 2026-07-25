using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class JsonSerializerDiagnosticTests
{
    [Test]
    public void JsonSerializerGeneratorDoesNotReportTaggedUnionDiagnostics()
    {
        var (diagnostics, generatedCodes, _, _) = Helper
            .CompileAndGetResults<TaggedUnionJsonSerializerGenerator>(
                sourceCode:
                """
                namespace Macaron.Union.Tests;

                [TaggedUnion]
                [TaggedUnionJsonSerializer]
                public readonly partial record struct Foo : ITaggedUnionCases<int, string>
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
        var errorDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(errorDiagnostics, Is.Empty);
            Assert.That(generatedCodes, Is.Empty);
        });
    }

    [Test]
    public void JsonSerializerAttributeWithoutTaggedUnionAttribute()
    {
        var (diagnostics, generatedCodes, _, _) = Helper
            .CompileAndGetResults<TaggedUnionJsonSerializerGenerator>(
                sourceCode:
                """
                namespace Macaron.Union.Tests;

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
        var errorDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.That(errorDiagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MTUJS0001" }));

        var diagnostic = errorDiagnostics.Single();

        Assert.Multiple(() =>
        {
            Assert.That(diagnostic.Location.SourceTree, Is.Not.Null);
            Assert.That(
                diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan),
                Is.EqualTo("TaggedUnionJsonSerializer")
            );
            Assert.That(generatedCodes, Is.Empty);
        });
    }

    [Test]
    public void JsonSerializerGeneratorDoesNotReportMissingTaggedUnionCasesInterfaceDiagnostic()
    {
        var (diagnostics, generatedCodes, _, _) = Helper
            .CompileAndGetResults<TaggedUnionJsonSerializerGenerator>(
                sourceCode:
                """
                namespace Macaron.Union.Tests;

                [TaggedUnion]
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
        var errorDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(errorDiagnostics, Is.Empty);
            Assert.That(generatedCodes, Is.Empty);
        });
    }
}
