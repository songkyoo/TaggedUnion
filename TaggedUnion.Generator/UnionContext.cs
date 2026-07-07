using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.TaggedUnion;

public sealed record UnionContext(
    INamedTypeSymbol TypeSymbol,
    string TypeName,
    ImmutableArray<UnionCaseContext> CaseContexts
);
