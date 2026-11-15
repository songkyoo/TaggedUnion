using Microsoft.CodeAnalysis;

using static Macaron.Union.Tests.Helper;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class SourceGenerationTests
{
    [Test]
    public void HintNameIsStable()
    {
        var (_, _, generatedHintNames, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );

        Assert.That(generatedHintNames, Has.Length.EqualTo(1));
        Assert.That(generatedHintNames[0], Is.EqualTo("Foo_0.ba8c0e3e.g.cs"));
    }

    [Test]
    public void GeneratedDeclarationMakesTargetTypeReadonly()
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);

        AssertNoErrors(outputCompilation.GetDiagnostics().Concat(generatorDiagnostics));

        var targetType = outputCompilation.GetTypeByMetadataName("Macaron.Union.Tests.Foo");
        var taggedUnionInterface = outputCompilation
            .GetTypeByMetadataName("Macaron.Union.ITaggedUnion`2")!
            .Construct(
                outputCompilation.GetSpecialType(SpecialType.System_Int32),
                outputCompilation.GetSpecialType(SpecialType.System_String)
            );

        Assert.Multiple(() =>
        {
            Assert.That(targetType, Is.Not.Null);
            Assert.That(targetType!.IsReadOnly, Is.True);
            Assert.That(
                targetType.AllInterfaces.Any(
                    type => SymbolEqualityComparer.Default.Equals(type, taggedUnionInterface)
                ),
                Is.True
            );
        });
    }

    [Test]
    public void ReferenceCasesShareStorage()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public class Qux<T>
            {
            }

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<Qux<int>, string>
            {
            }
            """,
            expectedFragments:
            [
                """
                #region Fields
                        private readonly byte _tag;
                        private readonly object? _reference;
                        #endregion
                """,
                "private global::Macaron.Union.Tests.Qux<int> __1 => (global::Macaron.Union.Tests.Qux<int>)_reference!;",
                "private string __2 => (string)_reference!;",
                "out global::Macaron.Union.Tests.Qux<int>? value",
                "global::System.Action<global::Macaron.Union.Tests.Qux<int>> qux"
            ]
        );
    }

    [Test]
    public void UnmanagedCasesOverlapAtOffsetZero()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public readonly struct Qux
            {
                public readonly int Value;
            }

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<Qux, int>
            {
            }
            """,
            expectedFragments:
            [
                "[global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Explicit)]",
                "[global::System.Runtime.InteropServices.FieldOffset(0)] public global::Macaron.Union.Tests.Qux Value1;",
                "[global::System.Runtime.InteropServices.FieldOffset(0)] public int Value2;",
                """
                #region Fields
                        private readonly byte _tag;
                        private readonly Unmanaged _unmanaged;
                        #endregion
                """,
                "private global::Macaron.Union.Tests.Qux __1 => _unmanaged.Value1;",
                "private int __2 => _unmanaged.Value2;"
            ]
        );
    }

    [Test]
    public void ManagedCasesWithNestedReferencesHaveSeparateFields()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public readonly struct Baz
            {
                public readonly object Value;
            }

            public readonly struct Bar
            {
                public readonly Baz Value;
            }

            public readonly struct Qux
            {
                public readonly Baz Value;
            }

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<Qux, Bar>
            {
            }
            """,
            expectedFragments:
            [
                """
                #region Fields
                        private readonly byte _tag;
                        private readonly global::Macaron.Union.Tests.Qux _value1;
                        private readonly global::Macaron.Union.Tests.Bar _value2;
                        #endregion
                """,
                "private global::Macaron.Union.Tests.Qux __1 => _value1;",
                "private global::Macaron.Union.Tests.Bar __2 => _value2;"
            ]
        );
    }

    [Test]
    public void MixedStorageGeneratesCompleteUnion()
    {
        AssertGeneratedCode(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public readonly struct Qux
            {
                public readonly int Value;
            }

            public sealed class Quux
            {
                public float Value;
            }

            public readonly struct Bar
            {
                public readonly object Value;
            }

            public readonly struct Baz
            {
                public readonly string Value;
            }

            [TaggedUnion(GenerateConversionOperators = true)]
            public readonly partial struct Foo : ITaggedUnionCases<Qux, string, int, Bar, Quux, Baz>
            {
            }
            """,
            expected:
            """
            // <auto-generated />
            #nullable enable

            namespace Macaron.Union.Tests
            {
                readonly partial struct Foo : global::System.IEquatable<Foo>, global::Macaron.Union.ITaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>
                {
                    #region Types
                    [global::System.Runtime.InteropServices.StructLayout(global::System.Runtime.InteropServices.LayoutKind.Explicit)]
                    private struct Unmanaged
                    {
                        [global::System.Runtime.InteropServices.FieldOffset(0)] public global::Macaron.Union.Tests.Qux Value1;
                        [global::System.Runtime.InteropServices.FieldOffset(0)] public int Value3;
                    }
                    #endregion

                    #region Operator Overloads
                    public static bool operator ==(Foo left, Foo right)
                    {
                        return left.Equals(right);
                    }

                    public static bool operator !=(Foo left, Foo right)
                    {
                        return !left.Equals(right);
                    }
                    #endregion

                    #region Conversion Operators
                    public static implicit operator global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>(Foo value)
                    {
                        return value._tag switch
                        {
                            0 => default,
                            1 => new global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>(value.__1),
                            2 => new global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>(value.__2),
                            3 => new global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>(value.__3),
                            4 => new global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>(value.__4),
                            5 => new global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>(value.__5),
                            6 => new global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>(value.__6),
                            _ => throw new global::System.InvalidOperationException($"Invalid tag: {value._tag}"),
                        };
                    }

                    public static implicit operator Foo(global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz> value)
                    {
                        var tag = global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.Qux, string, int, global::Macaron.Union.Tests.Bar, global::Macaron.Union.Tests.Quux, global::Macaron.Union.Tests.Baz>.GetTag(in value);

                        return tag switch
                        {
                            0 => default,
                            1 => new Foo((global::Macaron.Union.Tests.Qux)value),
                            2 => new Foo((string)value),
                            3 => new Foo((int)value),
                            4 => new Foo((global::Macaron.Union.Tests.Bar)value),
                            5 => new Foo((global::Macaron.Union.Tests.Quux)value),
                            6 => new Foo((global::Macaron.Union.Tests.Baz)value),
                            _ => throw new global::System.InvalidOperationException($"Invalid tag: {tag}"),
                        };
                    }

                    public static implicit operator Foo(global::Macaron.Union.Tests.Qux value)
                    {
                        return new Foo(value);
                    }

                    public static explicit operator global::Macaron.Union.Tests.Qux(Foo value)
                    {
                        if (value.TryGetValue(out global::Macaron.Union.Tests.Qux result))
                        {
                            return result;
                        }

                        throw new global::System.InvalidCastException($"Unable to cast object of type '{typeof(Foo)}' to type '{typeof(global::Macaron.Union.Tests.Qux)}'.");
                    }

                    public static implicit operator Foo(string value)
                    {
                        return new Foo(value);
                    }

                    public static explicit operator string(Foo value)
                    {
                        if (value.TryGetValue(out string? result))
                        {
                            return result;
                        }

                        throw new global::System.InvalidCastException($"Unable to cast object of type '{typeof(Foo)}' to type '{typeof(string)}'.");
                    }

                    public static implicit operator Foo(int value)
                    {
                        return new Foo(value);
                    }

                    public static explicit operator int(Foo value)
                    {
                        if (value.TryGetValue(out int result))
                        {
                            return result;
                        }

                        throw new global::System.InvalidCastException($"Unable to cast object of type '{typeof(Foo)}' to type '{typeof(int)}'.");
                    }

                    public static implicit operator Foo(global::Macaron.Union.Tests.Bar value)
                    {
                        return new Foo(value);
                    }

                    public static explicit operator global::Macaron.Union.Tests.Bar(Foo value)
                    {
                        if (value.TryGetValue(out global::Macaron.Union.Tests.Bar result))
                        {
                            return result;
                        }

                        throw new global::System.InvalidCastException($"Unable to cast object of type '{typeof(Foo)}' to type '{typeof(global::Macaron.Union.Tests.Bar)}'.");
                    }

                    public static implicit operator Foo(global::Macaron.Union.Tests.Quux value)
                    {
                        return new Foo(value);
                    }

                    public static explicit operator global::Macaron.Union.Tests.Quux(Foo value)
                    {
                        if (value.TryGetValue(out global::Macaron.Union.Tests.Quux? result))
                        {
                            return result;
                        }

                        throw new global::System.InvalidCastException($"Unable to cast object of type '{typeof(Foo)}' to type '{typeof(global::Macaron.Union.Tests.Quux)}'.");
                    }

                    public static implicit operator Foo(global::Macaron.Union.Tests.Baz value)
                    {
                        return new Foo(value);
                    }

                    public static explicit operator global::Macaron.Union.Tests.Baz(Foo value)
                    {
                        if (value.TryGetValue(out global::Macaron.Union.Tests.Baz result))
                        {
                            return result;
                        }

                        throw new global::System.InvalidCastException($"Unable to cast object of type '{typeof(Foo)}' to type '{typeof(global::Macaron.Union.Tests.Baz)}'.");
                    }
                    #endregion

                    #region Fields
                    private readonly byte _tag;
                    private readonly object? _reference;
                    private readonly Unmanaged _unmanaged;
                    private readonly global::Macaron.Union.Tests.Bar _value4;
                    private readonly global::Macaron.Union.Tests.Baz _value6;
                    #endregion

                    #region Properties
                    public bool HasValue => _tag != 0;

                    public object? Value => _tag switch
                    {
                        0 => null,
                        1 => _unmanaged.Value1,
                        2 => _reference,
                        3 => _unmanaged.Value3,
                        4 => _value4,
                        5 => _reference,
                        6 => _value6,
                        _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
                    };

                    private global::Macaron.Union.Tests.Qux __1 => _unmanaged.Value1;
                    private string __2 => (string)_reference!;
                    private int __3 => _unmanaged.Value3;
                    private global::Macaron.Union.Tests.Bar __4 => _value4;
                    private global::Macaron.Union.Tests.Quux __5 => (global::Macaron.Union.Tests.Quux)_reference!;
                    private global::Macaron.Union.Tests.Baz __6 => _value6;
                    #endregion

                    #region Constructors
                    public Foo(global::Macaron.Union.Tests.Qux value)
                    {
                        _tag = 1;
                        _unmanaged.Value1 = value;
                    }

                    public Foo(string value)
                    {
                        if ((object?)value == null)
                        {
                            throw new global::System.ArgumentNullException(nameof(value));
                        }

                        _tag = 2;
                        _reference = value;
                    }

                    public Foo(int value)
                    {
                        _tag = 3;
                        _unmanaged.Value3 = value;
                    }

                    public Foo(global::Macaron.Union.Tests.Bar value)
                    {
                        _tag = 4;
                        _value4 = value;
                    }

                    public Foo(global::Macaron.Union.Tests.Quux value)
                    {
                        if ((object?)value == null)
                        {
                            throw new global::System.ArgumentNullException(nameof(value));
                        }

                        _tag = 5;
                        _reference = value;
                    }

                    public Foo(global::Macaron.Union.Tests.Baz value)
                    {
                        _tag = 6;
                        _value6 = value;
                    }
                    #endregion

                    #region Overrides
                    public override bool Equals(object? obj)
                    {
                        return obj is Foo other && Equals(other);
                    }

                    public override int GetHashCode()
                    {
                        return _tag switch
                        {
                            0 => 0,
                            1 => global::System.HashCode.Combine(_tag, _unmanaged.Value1),
                            2 => global::System.HashCode.Combine(_tag, _reference),
                            3 => global::System.HashCode.Combine(_tag, _unmanaged.Value3),
                            4 => global::System.HashCode.Combine(_tag, _value4),
                            5 => global::System.HashCode.Combine(_tag, _reference),
                            6 => global::System.HashCode.Combine(_tag, _value6),
                            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
                        };
                    }

                    public override string ToString()
                    {
                        return _tag switch
                        {
                            0 => "<Uninitialized>",
                            1 => $"{_unmanaged.Value1}",
                            2 => $"{_reference}",
                            3 => $"{_unmanaged.Value3}",
                            4 => $"{_value4}",
                            5 => $"{_reference}",
                            6 => $"{_value6}",
                            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
                        };
                    }
                    #endregion

                    #region IEquatable<Foo> Interface
                    public bool Equals(Foo other)
                    {
                        if (_tag != other._tag)
                        {
                            return false;
                        }

                        return _tag switch
                        {
                            0 => true,
                            1 => global::System.Collections.Generic.EqualityComparer<global::Macaron.Union.Tests.Qux>.Default.Equals(_unmanaged.Value1, other._unmanaged.Value1),
                            2 => global::System.Collections.Generic.EqualityComparer<string>.Default.Equals((string)_reference!, (string)other._reference!),
                            3 => global::System.Collections.Generic.EqualityComparer<int>.Default.Equals(_unmanaged.Value3, other._unmanaged.Value3),
                            4 => global::System.Collections.Generic.EqualityComparer<global::Macaron.Union.Tests.Bar>.Default.Equals(_value4, other._value4),
                            5 => global::System.Collections.Generic.EqualityComparer<global::Macaron.Union.Tests.Quux>.Default.Equals((global::Macaron.Union.Tests.Quux)_reference!, (global::Macaron.Union.Tests.Quux)other._reference!),
                            6 => global::System.Collections.Generic.EqualityComparer<global::Macaron.Union.Tests.Baz>.Default.Equals(_value6, other._value6),
                            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
                        };
                    }
                    #endregion

                    #region Methods
                    public TValue Get<TValue>() where TValue : notnull
                    {
                        switch (_tag)
                        {
                            case 1 when typeof(TValue) == typeof(global::Macaron.Union.Tests.Qux):
                            {
                                var value = __1;
                                return global::System.Runtime.CompilerServices.Unsafe.As<global::Macaron.Union.Tests.Qux, TValue>(ref value);
                            }
                            case 2 when typeof(TValue) == typeof(string):
                            {
                                var value = __2;
                                return global::System.Runtime.CompilerServices.Unsafe.As<string, TValue>(ref value);
                            }
                            case 3 when typeof(TValue) == typeof(int):
                            {
                                var value = __3;
                                return global::System.Runtime.CompilerServices.Unsafe.As<int, TValue>(ref value);
                            }
                            case 4 when typeof(TValue) == typeof(global::Macaron.Union.Tests.Bar):
                            {
                                var value = __4;
                                return global::System.Runtime.CompilerServices.Unsafe.As<global::Macaron.Union.Tests.Bar, TValue>(ref value);
                            }
                            case 5 when typeof(TValue) == typeof(global::Macaron.Union.Tests.Quux):
                            {
                                var value = __5;
                                return global::System.Runtime.CompilerServices.Unsafe.As<global::Macaron.Union.Tests.Quux, TValue>(ref value);
                            }
                            case 6 when typeof(TValue) == typeof(global::Macaron.Union.Tests.Baz):
                            {
                                var value = __6;
                                return global::System.Runtime.CompilerServices.Unsafe.As<global::Macaron.Union.Tests.Baz, TValue>(ref value);
                            }
                        }

                        throw new global::System.InvalidCastException($"Unable to cast object of type '{typeof(Foo)}' to type '{typeof(TValue)}'.");
                    }

                    public bool TryGetValue(out global::Macaron.Union.Tests.Qux value)
                    {
                        if (_tag != 1)
                        {
                            value = default;

                            return false;
                        }
                        else
                        {
                            value = _unmanaged.Value1;

                            return true;
                        }
                    }

                    public bool TryGetValue([global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out string? value)
                    {
                        if (_tag != 2)
                        {
                            value = default;

                            return false;
                        }
                        else
                        {
                            value = (string)_reference!;

                            return true;
                        }
                    }

                    public bool TryGetValue(out int value)
                    {
                        if (_tag != 3)
                        {
                            value = default;

                            return false;
                        }
                        else
                        {
                            value = _unmanaged.Value3;

                            return true;
                        }
                    }

                    public bool TryGetValue(out global::Macaron.Union.Tests.Bar value)
                    {
                        if (_tag != 4)
                        {
                            value = default;

                            return false;
                        }
                        else
                        {
                            value = _value4;

                            return true;
                        }
                    }

                    public bool TryGetValue([global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out global::Macaron.Union.Tests.Quux? value)
                    {
                        if (_tag != 5)
                        {
                            value = default;

                            return false;
                        }
                        else
                        {
                            value = (global::Macaron.Union.Tests.Quux)_reference!;

                            return true;
                        }
                    }

                    public bool TryGetValue(out global::Macaron.Union.Tests.Baz value)
                    {
                        if (_tag != 6)
                        {
                            value = default;

                            return false;
                        }
                        else
                        {
                            value = _value6;

                            return true;
                        }
                    }

                    public void Switch(
                        global::System.Action<global::Macaron.Union.Tests.Qux> qux,
                        global::System.Action<string> @string,
                        global::System.Action<int> @int,
                        global::System.Action<global::Macaron.Union.Tests.Bar> bar,
                        global::System.Action<global::Macaron.Union.Tests.Quux> quux,
                        global::System.Action<global::Macaron.Union.Tests.Baz> baz
                    )
                    {
                        switch (this._tag)
                        {
                            case 0: throw new global::System.InvalidOperationException("Value not initialized.");
                            case 1: qux(this._unmanaged.Value1); return;
                            case 2: @string((string)this._reference!); return;
                            case 3: @int(this._unmanaged.Value3); return;
                            case 4: bar(this._value4); return;
                            case 5: quux((global::Macaron.Union.Tests.Quux)this._reference!); return;
                            case 6: baz(this._value6); return;
                            default: throw new global::System.InvalidOperationException($"Invalid tag: {this._tag}");
                        }
                    }

                    public void Switch<TState>(
                        TState state,
                        global::System.Action<global::Macaron.Union.Tests.Qux, TState> qux,
                        global::System.Action<string, TState> @string,
                        global::System.Action<int, TState> @int,
                        global::System.Action<global::Macaron.Union.Tests.Bar, TState> bar,
                        global::System.Action<global::Macaron.Union.Tests.Quux, TState> quux,
                        global::System.Action<global::Macaron.Union.Tests.Baz, TState> baz
                    )
                    {
                        switch (this._tag)
                        {
                            case 0: throw new global::System.InvalidOperationException("Value not initialized.");
                            case 1: qux(this._unmanaged.Value1, state); return;
                            case 2: @string((string)this._reference!, state); return;
                            case 3: @int(this._unmanaged.Value3, state); return;
                            case 4: bar(this._value4, state); return;
                            case 5: quux((global::Macaron.Union.Tests.Quux)this._reference!, state); return;
                            case 6: baz(this._value6, state); return;
                            default: throw new global::System.InvalidOperationException($"Invalid tag: {this._tag}");
                        }
                    }

                    public TResult Match<TResult>(
                        global::System.Func<global::Macaron.Union.Tests.Qux, TResult> qux,
                        global::System.Func<string, TResult> @string,
                        global::System.Func<int, TResult> @int,
                        global::System.Func<global::Macaron.Union.Tests.Bar, TResult> bar,
                        global::System.Func<global::Macaron.Union.Tests.Quux, TResult> quux,
                        global::System.Func<global::Macaron.Union.Tests.Baz, TResult> baz
                    )
                    {
                        return this._tag switch
                        {
                            0 => throw new global::System.InvalidOperationException("Value not initialized."),
                            1 => qux(this._unmanaged.Value1),
                            2 => @string((string)this._reference!),
                            3 => @int(this._unmanaged.Value3),
                            4 => bar(this._value4),
                            5 => quux((global::Macaron.Union.Tests.Quux)this._reference!),
                            6 => baz(this._value6),
                            _ => throw new global::System.InvalidOperationException($"Invalid tag: {this._tag}"),
                        };
                    }

                    public TResult Match<TState, TResult>(
                        TState state,
                        global::System.Func<global::Macaron.Union.Tests.Qux, TState, TResult> qux,
                        global::System.Func<string, TState, TResult> @string,
                        global::System.Func<int, TState, TResult> @int,
                        global::System.Func<global::Macaron.Union.Tests.Bar, TState, TResult> bar,
                        global::System.Func<global::Macaron.Union.Tests.Quux, TState, TResult> quux,
                        global::System.Func<global::Macaron.Union.Tests.Baz, TState, TResult> baz
                    )
                    {
                        return this._tag switch
                        {
                            0 => throw new global::System.InvalidOperationException("Value not initialized."),
                            1 => qux(this._unmanaged.Value1, state),
                            2 => @string((string)this._reference!, state),
                            3 => @int(this._unmanaged.Value3, state),
                            4 => bar(this._value4, state),
                            5 => quux((global::Macaron.Union.Tests.Quux)this._reference!, state),
                            6 => baz(this._value6, state),
                            _ => throw new global::System.InvalidOperationException($"Invalid tag: {this._tag}"),
                        };
                    }
                    #endregion
                }
            }

            """
        );
    }

    [Test]
    public void CaseAttributeOverridesCaseTag()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            [TaggedUnionCase(typeof(int), tag: 42)]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            expectedFragments:
            [
                "public int Value42;",
                "private int __42 => _unmanaged.Value42;",
                "_tag = 42;",
                "if (_tag != 42)",
                "case 42: @int(",
                "42 => @int("
            ]
        );
    }

    [Test]
    public void CaseAttributeOverridesCaseParameterName()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public class Qux<T>
            {
            }

            [TaggedUnion]
            [TaggedUnionCase(typeof(Qux<int>), "baz")]
            public readonly partial struct Foo : ITaggedUnionCases<Qux<int>, string>
            {
            }
            """,
            expectedFragments:
            [
                "_tag = 1;",
                "global::System.Action<global::Macaron.Union.Tests.Qux<int>> baz",
                "global::System.Func<global::Macaron.Union.Tests.Qux<int>, TResult> baz"
            ]
        );
    }

    [Test]
    public void StateParameterCanShadowTheGeneratedTag()
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public sealed class State
            {
            }

            [TaggedUnion(StateParamName = "_tag")]
            public partial struct Foo : ITaggedUnionCases<State, string>
            {
            }

            public static class Scenario
            {
                public static string Format(Foo value)
                {
                    return value.Match("_", static (_, state) => state, static (_, state) => state);
                }
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        var updatedDriver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out var outputCompilation,
            out var generatorDiagnostics
        );

        AssertNoErrors(outputCompilation.GetDiagnostics().Concat(generatorDiagnostics));

        var generatedCode = updatedDriver.GetRunResult().GeneratedTrees.Single().GetText().ToString();

        AssertContainsFragments(
            generatedCode,
            """
            public void Switch<TState>(
                        TState _tag,
                        global::System.Action<global::Macaron.Union.Tests.State, TState> state,
                        global::System.Action<string, TState> @string
            """,
            "switch (this._tag)",
            "case 1: state((global::Macaron.Union.Tests.State)this._reference!, _tag); return;",
            """
            public TResult Match<TState, TResult>(
                        TState _tag,
                        global::System.Func<global::Macaron.Union.Tests.State, TState, TResult> state,
                        global::System.Func<string, TState, TResult> @string
            """,
            "1 => state((global::Macaron.Union.Tests.State)this._reference!, _tag),"
        );
    }

    [Test]
    public void InterfaceCaseParameterNameRemovesLeadingI()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public interface IQux
            {
            }

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<IQux, string>
            {
            }
            """,
            expectedFragments:
            [
                "global::System.Action<global::Macaron.Union.Tests.IQux> qux",
                "global::System.Func<global::Macaron.Union.Tests.IQux, TResult> qux"
            ]
        );
    }

    [Test]
    public void InterfaceCaseTypePreventsAllCaseConversionOperators()
    {
        var (_, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public interface IQux
            {
            }

            [TaggedUnion(GenerateConversionOperators = true)]
            public readonly partial struct Foo : ITaggedUnionCases<IQux, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var generatedCode = generatedCodes.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                generatedCode,
                Does.Not.Contain("implicit operator Foo(global::Macaron.Union.Tests.IQux value)")
            );
            Assert.That(
                generatedCode,
                Does.Not.Contain("explicit operator global::Macaron.Union.Tests.IQux(Foo value)")
            );
            Assert.That(generatedCode, Does.Not.Contain("implicit operator Foo(string value)"));
            Assert.That(generatedCode, Does.Not.Contain("explicit operator string(Foo value)"));
            Assert.That(
                generatedCode,
                Does.Contain(
                    "public static implicit operator global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.IQux, string>(Foo value)"
                )
            );
            Assert.That(
                generatedCode,
                Does.Contain("value.TryGetValue(out global::Macaron.Union.Tests.IQux? result1)")
            );
        });
    }

    [Test]
    public void CaseConversionOperatorsAreDisabledByDefault()
    {
        var (_, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var generatedCode = generatedCodes.Single();

        Assert.Multiple(() =>
        {
            Assert.That(generatedCode, Does.Contain("#region Conversion Operators"));
            Assert.That(
                generatedCode,
                Does.Contain(
                    "implicit operator global::Macaron.Union.TaggedUnion<int, string>(Foo value)"
                )
            );
            Assert.That(
                generatedCode,
                Does.Contain(
                    "implicit operator Foo(global::Macaron.Union.TaggedUnion<int, string> value)"
                )
            );
            Assert.That(generatedCode, Does.Not.Contain("implicit operator Foo(int value)"));
            Assert.That(generatedCode, Does.Not.Contain("implicit operator Foo(string value)"));
            Assert.That(generatedCode, Does.Not.Contain("explicit operator"));
            Assert.That(generatedCode, Does.Contain("public Foo(int value)"));
            Assert.That(generatedCode, Does.Contain("public Foo(string value)"));
        });
    }

    [TestCase("Get", "")]
    [TestCase("Foo", "public string Get<T>() => \"existing\";")]
    public void GeneratedGetPreservesExistingDeclarations(string typeName, string members)
    {
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode:
            $$"""
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public partial struct {{typeName}} : ITaggedUnionCases<int, string>
            {
                {{members}}
            }
            """,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly],
            assemblyName: "Macaron.TaggedUnion.Tests.GeneratedGetExistingDeclarations",
            generators: [new TaggedUnionGenerator().AsSourceGenerator()]
        );

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var generatorDiagnostics);
        AssertNoErrors(outputCompilation.GetDiagnostics().Concat(generatorDiagnostics));
    }

    [Test]
    public void NullableCaseParameterNameRemovesNullableAnnotation()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo : ITaggedUnionCases<int?[], string>
            {
            }
            """,
            expectedFragments:
            [
                "global::System.Action<int?[]> intArray",
                "global::System.Func<int?[], TResult> intArray"
            ]
        );
    }

    [Test]
    public void TaggedUnionCasesInterfaceCanBeDeclaredOnAnotherPartialDeclaration()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            [TaggedUnion]
            public readonly partial struct Foo
            {
            }

            public readonly partial struct Foo : ITaggedUnionCases<int, string>
            {
            }
            """,
            expectedFragments:
            [
                "public Foo(int value)",
                "public Foo(string value)"
            ]
        );
    }

    [Test]
    public void EightCaseTaggedUnionCasesInterface()
    {
        AssertGeneratedCodeContains(
            sourceCode:
            """
            namespace Macaron.Union.Tests;

            public sealed class C1 { }
            public sealed class C2 { }
            public sealed class C3 { }
            public sealed class C4 { }
            public sealed class C5 { }
            public sealed class C6 { }
            public sealed class C7 { }
            public sealed class C8 { }

            [TaggedUnion]
            public readonly partial struct Foo
                : ITaggedUnionCases<C1, C2, C3, C4, C5, C6, C7, C8>
            {
            }
            """,
            expectedFragments:
            [
                "implicit operator global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.C1, global::Macaron.Union.Tests.C2, global::Macaron.Union.Tests.C3, global::Macaron.Union.Tests.C4, global::Macaron.Union.Tests.C5, global::Macaron.Union.Tests.C6, global::Macaron.Union.Tests.C7, global::Macaron.Union.Tests.C8>(Foo value)",
                "implicit operator Foo(global::Macaron.Union.TaggedUnion<global::Macaron.Union.Tests.C1, global::Macaron.Union.Tests.C2, global::Macaron.Union.Tests.C3, global::Macaron.Union.Tests.C4, global::Macaron.Union.Tests.C5, global::Macaron.Union.Tests.C6, global::Macaron.Union.Tests.C7, global::Macaron.Union.Tests.C8> value)",
                "global::Macaron.Union.ITaggedUnion<global::Macaron.Union.Tests.C1, global::Macaron.Union.Tests.C2, global::Macaron.Union.Tests.C3, global::Macaron.Union.Tests.C4, global::Macaron.Union.Tests.C5, global::Macaron.Union.Tests.C6, global::Macaron.Union.Tests.C7, global::Macaron.Union.Tests.C8>",
                "public Foo(global::Macaron.Union.Tests.C1 value)",
                "public Foo(global::Macaron.Union.Tests.C8 value)"
            ]
        );
    }

    [Test]
    public void OfficialUnionSupportIsGeneratedForTaggedUnionCasesInterfaceContract()
    {
        var (diagnostics, generatedCodes, _, supportsOfficialUnion) =
            CompileAndGetResults<TaggedUnionGenerator>(
                sourceCode:
                """
                #nullable enable

                namespace System.Runtime.CompilerServices
                {
                    [global::System.AttributeUsage(
                        global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct)]
                    public sealed class UnionAttribute : global::System.Attribute
                    {
                    }

                    public interface IUnion
                    {
                        object? Value { get; }
                    }
                }

                namespace Macaron.Union.Tests
                {
                    [TaggedUnion]
                    public readonly partial struct Foo : ITaggedUnionCases<int, string>
                    {
                    }
                }
                """,
                additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
            );
        var generatedCode = generatedCodes.Single();

        Assert.Multiple(() =>
        {
            Assert.That(
                diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                Is.Empty
            );
            Assert.That(supportsOfficialUnion, Is.True);
            Assert.That(
                generatedCode,
                Does.Contain("[global::System.Runtime.CompilerServices.Union]")
            );
            Assert.That(
                generatedCode,
                Does.Contain(
                    "readonly partial struct Foo : global::System.IEquatable<Foo>, global::Macaron.Union.ITaggedUnion<int, string>, global::System.Runtime.CompilerServices.IUnion"
                )
            );
        });
    }
}
