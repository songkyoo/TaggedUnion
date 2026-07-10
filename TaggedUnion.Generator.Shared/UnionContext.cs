using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal sealed record UnionContext(
    INamedTypeSymbol TypeSymbol,
    string TypeName,
    bool SupportsOfficialUnion,
    ImmutableArray<UnionCaseContext> CaseContexts
);
