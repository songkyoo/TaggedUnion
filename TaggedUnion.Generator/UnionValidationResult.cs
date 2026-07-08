using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal abstract record UnionValidationResult
{
    public sealed record Failure(ImmutableArray<Diagnostic> Diagnostics) : UnionValidationResult;

    public sealed record Success(UnionContext Context) : UnionValidationResult;
}
