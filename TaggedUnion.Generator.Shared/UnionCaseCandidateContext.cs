using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal sealed record UnionCaseCandidateContext(
    Location TypeLocation,
    ITypeSymbol TypeSymbol,
    string ParamName,
    Location ParamNameLocation,
    bool IsParamNameExplicit,
    bool IsParamNameValid,
    byte Tag,
    Location TagLocation,
    bool IsTagExplicit
);
