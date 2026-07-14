using System.Collections.Immutable;

namespace Macaron.Union;

internal sealed record UnionGenerationModel(
    bool SupportsOfficialUnion,
    string Namespace,
    ImmutableArray<string> ContainingTypes,
    string TypeName,
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

        return SupportsOfficialUnion == other.SupportsOfficialUnion
            && StringComparer.Ordinal.Equals(Namespace, other.Namespace)
            && SequenceEqual(ContainingTypes, other.ContainingTypes, StringComparer.Ordinal)
            && StringComparer.Ordinal.Equals(TypeName, other.TypeName)
            && SequenceEqual(Cases, other.Cases, EqualityComparer<UnionCaseGenerationModel>.Default)
            && StringComparer.Ordinal.Equals(HintName, other.HintName);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = SupportsOfficialUnion.GetHashCode();

            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Namespace);
            hashCode = AddValuesHashCode(hashCode, ContainingTypes, StringComparer.Ordinal);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(TypeName);
            hashCode = AddValuesHashCode(hashCode, Cases, EqualityComparer<UnionCaseGenerationModel>.Default);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(HintName);

            return hashCode;
        }
    }
    #endregion

    #region Static Methods
    private static bool SequenceEqual<T>(ImmutableArray<T> x, ImmutableArray<T> y, IEqualityComparer<T> comparer)
    {
        if (x.IsDefault || y.IsDefault)
        {
            return x.IsDefault == y.IsDefault;
        }

        if (x.Length != y.Length)
        {
            return false;
        }

        for (var i = 0; i < x.Length; i++)
        {
            if (!comparer.Equals(x[i], y[i]))
            {
                return false;
            }
        }

        return true;
    }

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
