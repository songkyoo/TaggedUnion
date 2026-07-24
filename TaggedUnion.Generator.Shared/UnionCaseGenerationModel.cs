namespace Macaron.Union;

internal sealed record UnionCaseGenerationModel(
    UnionCaseStorageKind StorageKind,
    bool IsReferenceType,
    bool SupportsConversionOperators,
    string FullyQualifiedTypeName,
    string ParamName,
    byte Tag
);
