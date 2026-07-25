using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class DiagnosticTests
{
    [Test]
    public void RecordStructTargetType()
    {
        var (diagnostics, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial record struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var errorDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(errorDiagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "MTU0001" }));
            Assert.That(generatedCodes, Is.Empty);
        });
    }

    [Test]
    public void NonStructTargetTypeUsesCompilerDiagnostic()
    {
        var (diagnostics, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public class Foo : ITaggedUnion<int, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var errorDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(errorDiagnostics.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { "CS0592" }));
            Assert.That(generatedCodes, Is.Empty);
        });
    }

    [Test]
    public void GenericTargetType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo<T> : ITaggedUnion<int, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0002"
        );
    }

    [Test]
    public void TargetTypeWithInstanceConstructor()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
                public Foo()
                {
                }
            }
            """,
            expectedDiagnosticId: "MTU0003"
        );
    }

    [Test]
    public void VoidCaseType()
    {
        AssertDiagnostics(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<void, string>
            {
            }
            """,
            expectedDiagnosticIds:
            [
                "CS0306",
                "CS1547",
                "MTU0004",
            ]
        );
    }

    [Test]
    public void ObjectCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<object, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0004"
        );
    }

    [Test]
    public void NullableCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<int?, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0004"
        );
    }

    [Test]
    public void NullableReferenceCaseType()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            #nullable enable

            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<string?, int>
            {
            }
            """,
            expectedDiagnosticId: "MTU0004",
            expectedLocationText: "string?"
        );
    }

    [Test]
    public void NullableReferenceArrayCaseType()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            #nullable enable

            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<int[]?, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0004",
            expectedLocationText: "int[]?"
        );
    }

    [Test]
    public void NestedNullableTypesAreSupported()
    {
        var (diagnostics, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode:
            """
            #nullable enable

            using System.Collections.Generic;

            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<int?[], List<string?>>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error),
                Is.Empty
            );
            Assert.That(generatedCodes, Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void UnboundGenericCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using System.Collections.Generic;

            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<List<>, string>
            {
            }
            """,
            expectedDiagnosticId: "CS7003"
        );
    }

    [Test]
    public void RefLikeCaseType()
    {
        AssertDiagnostics(
            sourceCode:
            """
            using System.Collections.Generic;

            namespace Macaron.Union.Tests;

            ref struct Qux
            {
            }

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<Qux, string>
            {
            }
            """,
            expectedDiagnosticIds:
            [
                "CS9244",
                "MTU0004",
            ]
        );
    }

    [Test]
    public void DuplicateCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnion<string, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0005"
        );
    }

    [Test]
    public void DuplicateCaseParameterName()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests.Left
            {
                public class Qux
                {
                }
            }

            namespace Macaron.Union.Tests.Right
            {
                public class Qux
                {
                }
            }

            namespace Macaron.Union.Tests
            {
                [TaggedUnion]
                public readonly partial struct Foo : ITaggedUnion<Left.Qux, Right.Qux>
                {
                }
            }
            """,
            expectedDiagnosticId: "MTU0006"
        );
    }

    [Test]
    public void DuplicateCaseParameterNameFromCaseAttribute()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public class Qux
            {
            }

            [TaggedUnion]
            [TaggedUnionCase(typeof(Qux), "string")]
            public readonly partial struct Foo : ITaggedUnion<Qux, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0006",
            expectedLocationText: "\"string\""
        );
    }

    [Test]
    public void DuplicateCaseParameterNameBetweenCaseAttributes()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests.Left
            {
                public class Qux
                {
                }
            }

            namespace Macaron.Union.Tests.Right
            {
                public class Bar
                {
                }
            }

            namespace Macaron.Union.Tests
            {
                [TaggedUnion]
                [TaggedUnionCase(typeof(Left.Qux), "value")]
                [TaggedUnionCase(typeof(Right.Bar), @"value")]
                public readonly partial struct Foo : ITaggedUnion<Left.Qux, Right.Bar>
                {
                }
            }
            """,
            expectedDiagnosticId: "MTU0006",
            expectedLocationText: "@\"value\""
        );
    }

    [Test]
    public void InvalidCaseParameterNameFromCaseAttribute()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public class Qux
            {
            }

            [TaggedUnion]
            [TaggedUnionCase(typeof(Qux), "bad-name")]
            public readonly partial struct Foo : ITaggedUnion<Qux, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0007",
            expectedLocationText: "\"bad-name\""
        );
    }

    [Test]
    public void CaseAttributeTypeNotSpecifiedInTaggedUnion()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public class Qux
            {
            }

            public class Baz
            {
            }

            [TaggedUnion]
            [TaggedUnionCase(typeof(Baz), "baz")]
            public readonly partial struct Foo : ITaggedUnion<Qux, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0008",
            expectedLocationText: "typeof(Baz)"
        );
    }

    [Test]
    public void CaseAttributeTypeNotSpecifiedInTaggedUnionWithoutParamName()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public class Qux
            {
            }

            public class Baz
            {
            }

            [TaggedUnion]
            [TaggedUnionCase(typeof(Baz))]
            public readonly partial struct Foo : ITaggedUnion<Qux, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0008",
            expectedLocationText: "typeof(Baz)"
        );
    }

    [Test]
    public void CaseAttributeWithoutTaggedUnionAttribute()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnionCase(typeof(int))]
            public readonly partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0011",
            expectedLocationText: "TaggedUnionCase(typeof(int))"
        );
    }

    [Test]
    public void TaggedUnionAttributeWithoutTaggedUnionInterface()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0012",
            expectedLocationText: "TaggedUnion"
        );
    }

    [Test]
    public void MultipleTaggedUnionInterfaces()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo
                : ITaggedUnion<int, string>, ITaggedUnion<long, bool>
            {
            }
            """,
            expectedDiagnosticId: "MTU0013",
            expectedLocationText: "ITaggedUnion<long, bool>"
        );
    }

    [Test]
    public void IndirectTaggedUnionInterfaceImplementation()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public interface IFooUnion : ITaggedUnion<int, string>
            {
            }

            [TaggedUnion]
            public readonly partial struct Foo : IFooUnion
            {
            }
            """,
            expectedDiagnosticId: "MTU0012",
            expectedLocationText: "TaggedUnion"
        );
    }

    [Test]
    public void TaggedUnionInterfaceWithoutTaggedUnionAttributeIsIgnored()
    {
        var (diagnostics, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Where(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error),
                Is.Empty
            );
            Assert.That(generatedCodes, Is.Empty);
        });
    }

    [Test]
    public void UnresolvedCaseAttributeType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public class Qux
            {
            }

            [TaggedUnion]
            [TaggedUnionCase(typeof(Missing), "missing")]
            public readonly partial struct Foo : ITaggedUnion<Qux, string>
            {
            }
            """,
            expectedDiagnosticId: "CS0246"
        );
    }

    [Test]
    public void ExplicitZeroCaseTag()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(int), tag: 0)]
            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0009",
            expectedLocationText: "0"
        );
    }

    [Test]
    public void NegativeCaseTag()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(int), tag: -1)]
            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            expectedDiagnosticId: "CS1503"
        );
    }

    [Test]
    public void CaseTagAboveByteRange()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(int), tag: 256)]
            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            expectedDiagnosticId: "CS1503"
        );
    }

    [Test]
    public void DuplicateExplicitCaseTag()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(int), tag: 42)]
            [TaggedUnionCase(typeof(string), tag: 0x2A)]
            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0010",
            expectedLocationText: "0x2A"
        );
    }

    [Test]
    public void ExplicitCaseTagDuplicatesGeneratedCaseTag()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(int), tag: 2)]
            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0010",
            expectedLocationText: "2"
        );
    }

    [Test]
    public void ExplicitCaseTagDuplicatesEarlierGeneratedCaseTag()
    {
        AssertDiagnosticLocationText(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(string), tag: 0x01)]
            public readonly partial struct Foo : ITaggedUnion<int, string>
            {
            }
            """,
            expectedDiagnosticId: "MTU0010",
            expectedLocationText: "0x01"
        );
    }

    [Test]
    public void MultipleCaseTypeDiagnostics()
    {
        AssertDiagnostics(
            sourceCode:
            """
            namespace Macaron.Union.Tests.Left
            {
                public class Qux
                {
                }
            }

            namespace Macaron.Union.Tests.Right
            {
                public class Qux
                {
                }
            }

            namespace Macaron.Union.Tests
            {
                [TaggedUnion]
                public readonly partial struct Foo : ITaggedUnion<void, string, string, Left.Qux, Right.Qux>
                {
                }
            }
            """,
            expectedDiagnosticIds:
            [
                "CS0306",
                "CS1547",
                "MTU0004",
                "MTU0005",
                "MTU0006",
            ]
        );
    }
}
