namespace Macaron.Union;

#if NET11_0_OR_GREATER
[global::System.Runtime.CompilerServices.Union]
public readonly struct TaggedUnion<T1, T2> :
    global::System.IEquatable<TaggedUnion<T1, T2>>,
    ITaggedUnion<T1, T2>,
    TaggedUnion<T1, T2>.IUnionMembers
#else
public readonly struct TaggedUnion<T1, T2> :
    global::System.IEquatable<TaggedUnion<T1, T2>>,
    ITaggedUnion<T1, T2>
#endif
    where T1 : notnull
    where T2 : notnull
{
#if NET11_0_OR_GREATER
    #region Types
    public interface IUnionMembers
    {
        static TaggedUnion<T1, T2> Create(T1 value) => new(value);

        static TaggedUnion<T1, T2> Create(T2 value) => new(value);

        object? Value { get; }
    }
    #endregion
#endif

    #region Operator Overloads
    public static bool operator ==(TaggedUnion<T1, T2> left, TaggedUnion<T1, T2> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TaggedUnion<T1, T2> left, TaggedUnion<T1, T2> right)
    {
        return !left.Equals(right);
    }
    #endregion

    #region Conversion Operators
    public static implicit operator TaggedUnion<T1, T2>(T1 value)
    {
        return new TaggedUnion<T1, T2>(value);
    }

    public static explicit operator T1(TaggedUnion<T1, T2> value)
    {
        if (value.TryGetValue(out T1? result))
        {
            return result;
        }

        throw new global::System.InvalidCastException(
            $"Unable to cast object of type '{typeof(TaggedUnion<T1, T2>)}' to type '{typeof(T1)}'."
        );
    }

    public static implicit operator TaggedUnion<T1, T2>(T2 value)
    {
        return new TaggedUnion<T1, T2>(value);
    }

    public static explicit operator T2(TaggedUnion<T1, T2> value)
    {
        if (value.TryGetValue(out T2? result))
        {
            return result;
        }

        throw new global::System.InvalidCastException(
            $"Unable to cast object of type '{typeof(TaggedUnion<T1, T2>)}' to type '{typeof(T2)}'."
        );
    }
    #endregion

    #region Fields
    private readonly byte _tag;
    private readonly T1? _value1;
    private readonly T2? _value2;
    #endregion

    #region Properties
    public bool HasValue => _tag != 0;

    public object? Value => _tag switch
    {
        0 => null,
        1 => _value1,
        2 => _value2,
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
    #endregion

    #region Overrides
    public override bool Equals(object? obj)
    {
        return obj is TaggedUnion<T1, T2> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return _tag switch
        {
            0 => 0,
            1 => global::System.HashCode.Combine(_tag, _value1),
            2 => global::System.HashCode.Combine(_tag, _value2),
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
            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
        };
    }
    #endregion

    #region IEquatable<TaggedUnion<T1, T2>> Interface
    public bool Equals(TaggedUnion<T1, T2> other)
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
            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
        };
    }
    #endregion

    #region Methods
    [global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]
    public static byte GetTag(in TaggedUnion<T1, T2> value)
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

    public void Switch(
        global::System.Action<T1> t1,
        global::System.Action<T2> t2
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
            default:
                throw new global::System.InvalidOperationException($"Invalid tag: {_tag}");
        }
    }

    public TResult Match<TResult>(
        global::System.Func<T1, TResult> t1,
        global::System.Func<T2, TResult> t2
    )
    {
        return _tag switch
        {
            0 => throw new global::System.InvalidOperationException("Value not initialized."),
            1 => t1(_value1!),
            2 => t2(_value2!),
            _ => throw new global::System.InvalidOperationException($"Invalid tag: {_tag}"),
        };
    }
    #endregion
}
