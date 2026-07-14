using System.Collections.Immutable;

namespace Macaron.Union;

internal sealed class UnionGenerationModelComparer : IEqualityComparer<UnionGenerationModel>
{
    #region Fields
    public static readonly UnionGenerationModelComparer Instance = new();
    #endregion

    #region Constructors
    private UnionGenerationModelComparer()
    {
    }
    #endregion

    #region IEqualityComparer Interface
    public bool Equals(UnionGenerationModel? x, UnionGenerationModel? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null)
        {
            return false;
        }

        return x.SupportsOfficialUnion == y.SupportsOfficialUnion
            && StringComparer.Ordinal.Equals(x.Namespace, y.Namespace)
            && SequenceEqual(x.ContainingTypes, y.ContainingTypes, StringComparer.Ordinal)
            && StringComparer.Ordinal.Equals(x.TypeName, y.TypeName)
            && SequenceEqual(x.Cases, y.Cases, EqualityComparer<UnionCaseGenerationModel>.Default)
            && StringComparer.Ordinal.Equals(x.HintName, y.HintName);
    }

    public int GetHashCode(UnionGenerationModel obj)
    {
        unchecked
        {
            var hashCode = obj.SupportsOfficialUnion.GetHashCode();

            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(obj.Namespace);
            hashCode = AddValuesHashCode(hashCode, obj.ContainingTypes, StringComparer.Ordinal);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(obj.TypeName);
            hashCode = AddValuesHashCode(hashCode, obj.Cases, EqualityComparer<UnionCaseGenerationModel>.Default);
            hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(obj.HintName);

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
