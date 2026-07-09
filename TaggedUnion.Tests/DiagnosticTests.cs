using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class DiagnosticTests
{
    [Test]
    public void TargetTypeWithoutReadonly()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(int), typeof(string))]
            public partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0001"
        );
    }

    [Test]
    public void GenericTargetType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(int), typeof(string))]
            public readonly partial struct Foo<T>
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

            [TaggedUnion(typeof(int), typeof(string))]
            public readonly partial struct Foo
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
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(void), typeof(string))]
            public readonly partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0004"
        );
    }

    [Test]
    public void ObjectCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(object), typeof(string))]
            public readonly partial struct Foo
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

            [TaggedUnion(typeof(int?), typeof(string))]
            public readonly partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0004"
        );
    }

    [Test]
    public void UnboundGenericCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using System.Collections.Generic;

            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(List<>), typeof(string))]
            public readonly partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0004"
        );
    }

    [Test]
    public void RefLikeCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            using System.Collections.Generic;

            namespace Macaron.Union.Tests;

            ref struct Qux
            {
            }

            [TaggedUnion(typeof(Qux), typeof(string))]
            public readonly partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0004"
        );
    }

    [Test]
    public void DuplicateCaseType()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion(typeof(string), typeof(string))]
            public readonly partial struct Foo
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
                [TaggedUnion(typeof(Left.Qux), typeof(Right.Qux))]
                public readonly partial struct Foo
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

            [TaggedUnion(typeof(Qux), typeof(string))]
            [TaggedUnionCase(typeof(Qux), "string")]
            public readonly partial struct Foo
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
                [TaggedUnion(typeof(Left.Qux), typeof(Right.Bar))]
                [TaggedUnionCase(typeof(Left.Qux), "value")]
                [TaggedUnionCase(typeof(Right.Bar), @"value")]
                public readonly partial struct Foo
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

            [TaggedUnion(typeof(Qux), typeof(string))]
            [TaggedUnionCase(typeof(Qux), "bad-name")]
            public readonly partial struct Foo
            {
            }
            """,
            expectedDiagnosticId: "MTU0007",
            expectedLocationText: "\"bad-name\""
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
                [TaggedUnion(typeof(void), typeof(string), typeof(string), typeof(Left.Qux), typeof(Right.Qux))]
                public readonly partial struct Foo
                {
                }
            }
            """,
            expectedDiagnosticIds:
            [
                "MTU0004",
                "MTU0005",
                "MTU0006",
            ]
        );
    }
}
