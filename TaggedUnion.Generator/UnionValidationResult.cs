using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.TaggedUnion;

internal abstract record UnionValidationResult
{
    public sealed record CompilationError : UnionValidationResult;

    public sealed record Invalid(ImmutableArray<Diagnostic> Diagnostics) : UnionValidationResult;

    public sealed record Valid(UnionContext Context) : UnionValidationResult;
}
