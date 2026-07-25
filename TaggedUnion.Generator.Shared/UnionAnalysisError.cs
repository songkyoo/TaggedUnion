using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal abstract record UnionAnalysisError(Location Location)
{
    public sealed record TargetTypeMustBeReadOnlyPartialStruct(
        Location Location,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record TargetTypeMustBeNonGeneric(
        Location Location,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record TargetTypeCannotDeclareInstanceConstructors(
        Location Location,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record TargetTypeMustImplementTaggedUnionInterface(
        Location Location,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record TargetTypeCannotImplementMultipleTaggedUnionInterfaces(
        Location Location,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record UnsupportedCaseType(
        Location Location,
        string CaseTypeName,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record DuplicateCaseType(
        Location Location,
        string CaseTypeName,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record DuplicateCaseParameterName(
        Location Location,
        string CaseTypeName,
        string ParameterName,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record InvalidCaseParameterName(
        Location Location,
        string CaseTypeName,
        string ParameterName,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record CaseAttributeTypeMustMatchCaseType(
        Location Location,
        string CaseTypeName,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record InvalidCaseTag(
        Location Location,
        string CaseTypeName,
        byte Tag,
        string TargetTypeName
    ) : UnionAnalysisError(Location);

    public sealed record DuplicateCaseTag(
        Location Location,
        string CaseTypeName,
        byte Tag,
        string TargetTypeName
    ) : UnionAnalysisError(Location);
}
