namespace Macaron.Union;

#if NET11_0_OR_GREATER
[global::System.Runtime.CompilerServices.Union]
public readonly struct TaggedUnion<T1, T2, T3, T4, T5> :
    global::System.IEquatable<TaggedUnion<T1, T2, T3, T4, T5>>,
    ITaggedUnion<T1, T2, T3, T4, T5>,
    TaggedUnion<T1, T2, T3, T4, T5>.IUnionMembers
#else
public readonly struct TaggedUnion<T1, T2, T3, T4, T5> :
    global::System.IEquatable<TaggedUnion<T1, T2, T3, T4, T5>>,
    ITaggedUnion<T1, T2, T3, T4, T5>
#endif
    where T1 : notnull
    where T2 : notnull
    where T3 : notnull
    where T4 : notnull
    where T5 : notnull
{
#if NET11_0_OR_GREATER
    #region Types
    public interface IUnionMembers
    {
        static TaggedUnion<T1, T2, T3, T4, T5> Create(T1 value) => new(value);

        static TaggedUnion<T1, T2, T3, T4, T5> Create(T2 value) => new(value);

        static TaggedUnion<T1, T2, T3, T4, T5> Create(T3 value) => new(value);

        static TaggedUnion<T1, T2, T3, T4, T5> Create(T4 value) => new(value);

        static TaggedUnion<T1, T2, T3, T4, T5> Create(T5 value) => new(value);

        object? Value { get; }
    }
    #endregion
#endif

    #region Operator Overloads
    public static bool operator ==(TaggedUnion<T1, T2, T3, T4, T5> left, TaggedUnion<T1, T2, T3, T4, T5> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TaggedUnion<T1, T2, T3, T4, T5> left, TaggedUnion<T1, T2, T3, T4, T5> right)
    {
        return !left.Equals(right);
    }
    #endregion

    #region Conversion Operators
    public static implicit operator TaggedUnion<T1, T2, T3, T4, T5>(T1 value)
    {
        return new TaggedUnion<T1, T2, T3, T4, T5>(value);
    }

    public static explicit operator T1(TaggedUnion<T1, T2, T3, T4, T5> value)
    {
        if (value.TryGetValue(out T1? result))
        {
            return result;
        }

        throw new global::System.InvalidCastException(
            $"Unable to cast object of type '{typeof(TaggedUnion<T1, T2, T3, T4, T5>)}' to type '{typeof(T1)}'."
        );
    }

    public static implicit operator TaggedUnion<T1, T2, T3, T4, T5>(T2 value)
    {
        return new TaggedUnion<T1, T2, T3, T4, T5>(value);
    }

    public static explicit operator T2(TaggedUnion<T1, T2, T3, T4, T5> value)
    {
        if (value.TryGetValue(out T2? result))
        {
            return result;
        }

        throw new global::System.InvalidCastException(
            $"Unable to cast object of type '{typeof(TaggedUnion<T1, T2, T3, T4, T5>)}' to type '{typeof(T2)}'."
        );
    }

    public static implicit operator TaggedUnion<T1, T2, T3, T4, T5>(T3 value)
    {
        return new TaggedUnion<T1, T2, T3, T4, T5>(value);
    }

    public static explicit operator T3(TaggedUnion<T1, T2, T3, T4, T5> value)
    {
        if (value.TryGetValue(out T3? result))
        {
            return result;
        }

        throw new global::System.InvalidCastException(
            $"Unable to cast object of type '{typeof(TaggedUnion<T1, T2, T3, T4, T5>)}' to type '{typeof(T3)}'."
        );
    }

    public static implicit operator TaggedUnion<T1, T2, T3, T4, T5>(T4 value)
    {
        return new TaggedUnion<T1, T2, T3, T4, T5>(value);
    }

    public static explicit operator T4(TaggedUnion<T1, T2, T3, T4, T5> value)
    {
        if (value.TryGetValue(out T4? result))
        {
            return result;
        }

        throw new global::System.InvalidCastException(
            $"Unable to cast object of type '{typeof(TaggedUnion<T1, T2, T3, T4, T5>)}' to type '{typeof(T4)}'."
        );
    }

    public static implicit operator TaggedUnion<T1, T2, T3, T4, T5>(T5 value)
    {
        return new TaggedUnion<T1, T2, T3, T4, T5>(value);
    }

    public static explicit operator T5(TaggedUnion<T1, T2, T3, T4, T5> value)
    {
        if (value.TryGetValue(out T5? result))
        {
            return result;
        }

        throw new global::System.InvalidCastException(
            $"Unable to cast object of type '{typeof(TaggedUnion<T1, T2, T3, T4, T5>)}' to type '{typeof(T5)}'."
        );
    }
    #endregion

    #region Fields
    private readonly byte _tag;
    private readonly T1? _value1;
    private readonly T2? _value2;
    private readonly T3? _value3;
    private readonly T4? _value4;
    private readonly T5? _value5;
    #endregion

    #region Properties
    public bool HasValue => _tag != 0;

    public object? Value => _tag switch
    {
        0 => null,
        1 => _value1,
        2 => _value2,
        3 => _value3,
        4 => _value4,
        5 => _value5,
        _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
    };
    #endregion

    #region Constructors
    public TaggedUnion(T1 value)
    {
        if (value is null)
        {
            throw new global::System.ArgumentNullException(nameof(value));
        }

        _tag = 1;
        _value1 = value;
    }

    public TaggedUnion(T2 value)
    {
        if (value is null)
        {
            throw new global::System.ArgumentNullException(nameof(value));
        }

        _tag = 2;
        _value2 = value;
    }

    public TaggedUnion(T3 value)
    {
        if (value is null)
        {
            throw new global::System.ArgumentNullException(nameof(value));
        }

        _tag = 3;
        _value3 = value;
    }

    public TaggedUnion(T4 value)
    {
        if (value is null)
        {
            throw new global::System.ArgumentNullException(nameof(value));
        }

        _tag = 4;
        _value4 = value;
    }

    public TaggedUnion(T5 value)
    {
        if (value is null)
        {
            throw new global::System.ArgumentNullException(nameof(value));
        }

        _tag = 5;
        _value5 = value;
    }
    #endregion

    #region Overrides
    public override bool Equals(object? obj)
    {
        return obj is TaggedUnion<T1, T2, T3, T4, T5> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _tag switch
        {
            0 => 0,
            1 => global::System.HashCode.Combine(_tag, _value1),
            2 => global::System.HashCode.Combine(_tag, _value2),
            3 => global::System.HashCode.Combine(_tag, _value3),
            4 => global::System.HashCode.Combine(_tag, _value4),
            5 => global::System.HashCode.Combine(_tag, _value5),
            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
        };
    }

    public override string ToString()
    {
        return _tag switch
        {
            0 => "<Uninitialized>",
            1 => $"{_value1}",
            2 => $"{_value2}",
            3 => $"{_value3}",
            4 => $"{_value4}",
            5 => $"{_value5}",
            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
        };
    }
    #endregion

    #region IEquatable<TaggedUnion<T1, T2, T3, T4, T5>> Interface
    public bool Equals(TaggedUnion<T1, T2, T3, T4, T5> other)
    {
        if (_tag != other._tag)
        {
            return false;
        }

        return _tag switch
        {
            0 => true,
            1 => global::System.Collections.Generic.EqualityComparer<T1>.Default.Equals(
                _value1!,
                other._value1!
            ),
            2 => global::System.Collections.Generic.EqualityComparer<T2>.Default.Equals(
                _value2!,
                other._value2!
            ),
            3 => global::System.Collections.Generic.EqualityComparer<T3>.Default.Equals(
                _value3!,
                other._value3!
            ),
            4 => global::System.Collections.Generic.EqualityComparer<T4>.Default.Equals(
                _value4!,
                other._value4!
            ),
            5 => global::System.Collections.Generic.EqualityComparer<T5>.Default.Equals(
                _value5!,
                other._value5!
            ),
            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
        };
    }
    #endregion

    #region Methods
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public static byte GetTag(in TaggedUnion<T1, T2, T3, T4, T5> value)
    {
        return value._tag;
    }

    public bool TryGetValue(
        [global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T1? value
    )
    {
        if (_tag != 1)
        {
            value = default;

            return false;
        }

        value = _value1!;

        return true;
    }

    public bool TryGetValue(
        [global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T2? value
    )
    {
        if (_tag != 2)
        {
            value = default;

            return false;
        }

        value = _value2!;

        return true;
    }

    public bool TryGetValue(
        [global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T3? value
    )
    {
        if (_tag != 3)
        {
            value = default;

            return false;
        }

        value = _value3!;

        return true;
    }

    public bool TryGetValue(
        [global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T4? value
    )
    {
        if (_tag != 4)
        {
            value = default;

            return false;
        }

        value = _value4!;

        return true;
    }

    public bool TryGetValue(
        [global::System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T5? value
    )
    {
        if (_tag != 5)
        {
            value = default;

            return false;
        }

        value = _value5!;

        return true;
    }

    public void Switch(
        global::System.Action<T1> t1,
        global::System.Action<T2> t2,
        global::System.Action<T3> t3,
        global::System.Action<T4> t4,
        global::System.Action<T5> t5
    )
    {
        switch (_tag)
        {
            case 0:
                throw new global::System.InvalidOperationException("Value not initialized.");
            case 1:
                t1(_value1!);
                return;
            case 2:
                t2(_value2!);
                return;
            case 3:
                t3(_value3!);
                return;
            case 4:
                t4(_value4!);
                return;
            case 5:
                t5(_value5!);
                return;
            default:
                throw new global::System.InvalidOperationException($"Invalid tag: {_tag}");
        }
    }

    public TResult Match<TResult>(
        global::System.Func<T1, TResult> t1,
        global::System.Func<T2, TResult> t2,
        global::System.Func<T3, TResult> t3,
        global::System.Func<T4, TResult> t4,
        global::System.Func<T5, TResult> t5
    )
    {
        return _tag switch
        {
            0 => throw new global::System.InvalidOperationException("Value not initialized."),
            1 => t1(_value1!),
            2 => t2(_value2!),
            3 => t3(_value3!),
            4 => t4(_value4!),
            5 => t5(_value5!),
            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
        };
    }
    #endregion
}
