using System.Collections.Immutable;

namespace Macaron.Union;

internal sealed record UnionGenerationModel(
    bool SupportsOfficialUnion,
    string Namespace,
    ImmutableArray<string> ContainingTypes,
    string TypeName,
    ImmutableArray<UnionCaseGenerationModel> Cases,
    string HintName
);
