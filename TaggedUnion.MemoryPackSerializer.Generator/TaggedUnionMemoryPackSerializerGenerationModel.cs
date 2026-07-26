using System.Collections.Immutable;

namespace Macaron.Union;

internal sealed record TaggedUnionMemoryPackSerializerGenerationModel(
    UnionGenerationModel Union,
    ImmutableArray<MemoryPackUnionCaseGenerationModel> Cases
)
{
    #region Properties
    public string Namespace => Union.Namespace;

    public ImmutableArray<string> ContainingTypes => Union.ContainingTypes;

    public string TypeName => Union.TypeName;

    public string HintName => Union.HintName;
    #endregion

    #region IEquatable<TaggedUnionMemoryPackSerializerGenerationModel> Interface
    public bool Equals(TaggedUnionMemoryPackSerializerGenerationModel? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null
            || !EqualityComparer<UnionGenerationModel>.Default.Equals(Union, other.Union)
            || Cases.IsDefault != other.Cases.IsDefault
            || Cases.Length != other.Cases.Length
        )
        {
            return false;
        }

        for (var i = 0; i < Cases.Length; i++)
        {
            if (!EqualityComparer<MemoryPackUnionCaseGenerationModel>.Default.Equals(
                Cases[i],
                other.Cases[i]
            ))
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = EqualityComparer<UnionGenerationModel>.Default.GetHashCode(Union);

            if (Cases.IsDefault)
            {
                return (hashCode * 397) ^ -1;
            }

            foreach (var caseModel in Cases)
            {
                hashCode = (hashCode * 397) ^ EqualityComparer<MemoryPackUnionCaseGenerationModel>.Default.GetHashCode(caseModel);
            }

            return hashCode;
        }
    }
    #endregion
}
