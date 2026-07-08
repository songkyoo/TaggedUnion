using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal sealed record UnionContext(
    INamedTypeSymbol TypeSymbol,
    string TypeName,
    ImmutableArray<UnionCaseContext> CaseContexts
);
