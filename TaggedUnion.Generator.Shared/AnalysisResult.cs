using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal abstract record AnalysisResult
{
    public sealed record Success(UnionGenerationModel Model) : AnalysisResult;

    public sealed record Failure(ImmutableArray<Diagnostic> Diagnostics) : AnalysisResult;
}
