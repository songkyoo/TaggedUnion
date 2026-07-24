namespace Macaron.Union;

internal sealed record UnionCaseGenerationModel(
    bool IsReferenceType,
    bool IsUnmanagedType,
    bool SupportsConversionOperators,
    string FullyQualifiedTypeName,
    string ParamName,
    byte Tag
);
