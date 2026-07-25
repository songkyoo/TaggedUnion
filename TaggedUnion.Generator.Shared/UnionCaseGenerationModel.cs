namespace Macaron.Union;

internal sealed record UnionCaseGenerationModel(
    bool IsReferenceType,
    bool IsUnmanagedType,
    bool IsInterfaceType,
    string FullyQualifiedTypeName,
    string ParamName,
    byte Tag
);
