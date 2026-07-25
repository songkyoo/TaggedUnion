using System.Collections.Immutable;

namespace Macaron.Union;

internal sealed record TaggedUnionSourceGenerationModel(
    UnionGenerationModel Union,
    bool SupportsOfficialUnion
)
{
    public string Namespace => Union.Namespace;

    public ImmutableArray<string> ContainingTypes => Union.ContainingTypes;

    public string TypeName => Union.TypeName;

    public ImmutableArray<UnionCaseGenerationModel> Cases => Union.Cases;

    public string HintName => Union.HintName;
}
