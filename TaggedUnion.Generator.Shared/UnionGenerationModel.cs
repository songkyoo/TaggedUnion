using System.Collections.Immutable;

namespace Macaron.Union;

internal sealed record UnionGenerationModel(
    string Namespace,
    ImmutableArray<string> ContainingTypes,
    string TypeName,
    string StateParamName,
    ImmutableArray<UnionCaseGenerationModel> Cases,
    string HintName
)
{
    #region IEquatable<UnionGenerationModel> Interface
    public bool Equals(UnionGenerationModel? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null)
        {
            return false;
        }

        var stringComparer = StringComparer.Ordinal;

        return stringComparer.Equals(Namespace, other.Namespace)
            && ContainingTypes.SequenceEqual(other.ContainingTypes, stringComparer)
            && stringComparer.Equals(TypeName, other.TypeName)
            && stringComparer.Equals(StateParamName, other.StateParamName)
            && Cases.SequenceEqual(other.Cases, EqualityComparer<UnionCaseGenerationModel>.Default)
            && stringComparer.Equals(HintName, other.HintName);
    }

    public override int GetHashCode()
    {
        var stringComparer = StringComparer.Ordinal;

        unchecked
        {
            var hashCode = stringComparer.GetHashCode(Namespace);

            hashCode = AddValuesHashCode(hashCode, ContainingTypes, stringComparer);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(TypeName);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(StateParamName);
            hashCode = AddValuesHashCode(hashCode, Cases, EqualityComparer<UnionCaseGenerationModel>.Default);
            hashCode = (hashCode * 397) ^ stringComparer.GetHashCode(HintName);

            return hashCode;
        }
    }
    #endregion

    #region Static Methods
    private static int AddValuesHashCode<T>(int hashCode, ImmutableArray<T> values, IEqualityComparer<T> comparer)
    {
        unchecked
        {
            if (values.IsDefault)
            {
                return (hashCode * 397) ^ -1;
            }

            foreach (var value in values)
            {
                hashCode = (hashCode * 397) ^ comparer.GetHashCode(value);
            }

            return hashCode;
        }
    }
    #endregion
}
