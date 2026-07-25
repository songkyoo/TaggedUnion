using System.Collections.Immutable;

namespace Macaron.Union;

internal sealed record TaggedUnionSourceGenerationModel(
    TaggedUnionGenerationModel Model,
    bool SupportsOfficialUnion
)
{
    public string Namespace => Model.Union.Namespace;

    public ImmutableArray<string> ContainingTypes => Model.Union.ContainingTypes;

    public string TypeName => Model.Union.TypeName;

    public string StateParamName => Model.Union.StateParamName;

    public bool GenerateConversionOperators => Model.GenerateConversionOperators;

    public ImmutableArray<UnionCaseGenerationModel> Cases => Model.Union.Cases;

    public string HintName => Model.Union.HintName;
}
