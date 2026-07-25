using System.Collections.Immutable;

namespace Macaron.Union;

internal abstract record AnalysisResult
{
    public sealed record Success(UnionGenerationModel Model) : AnalysisResult;

    public sealed record Failure(ImmutableArray<UnionAnalysisError> Errors) : AnalysisResult
    {
        public static readonly Failure Empty = new Failure(ImmutableArray<UnionAnalysisError>.Empty);

        public Failure(UnionAnalysisError error) : this(ImmutableArray.Create(error))
        {
        }
    }
}
