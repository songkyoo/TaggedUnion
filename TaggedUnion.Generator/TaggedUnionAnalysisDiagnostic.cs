using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal abstract record TaggedUnionAnalysisDiagnostic(Location Location)
{
    public sealed record ConversionOperatorsNotSupportedForInterfaceCases(
        Location Location,
        string TargetTypeName,
        string InterfaceCaseTypeNames
    ) : TaggedUnionAnalysisDiagnostic(Location);
}
