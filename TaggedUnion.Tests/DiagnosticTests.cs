using static Macaron.TaggedUnion.Tests.Helper;

namespace Macaron.TaggedUnion.Tests;

[TestFixture]
public sealed class DiagnosticTests
{
    [Test]
    public void TargetTypeWithoutReadonly()
    {
        AssertDiagnostic(
            sourceCode:
            """
            namespace Macaron.TaggedUnion.Tests;

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
            namespace Macaron.TaggedUnion.Tests;

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
            namespace Macaron.TaggedUnion.Tests;

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
            namespace Macaron.TaggedUnion.Tests;

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
            namespace Macaron.TaggedUnion.Tests;

            [TaggedUnion(typeof(object), typeof(string))]
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

            namespace Macaron.TaggedUnion.Tests;

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

            namespace Macaron.TaggedUnion.Tests;

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
}
