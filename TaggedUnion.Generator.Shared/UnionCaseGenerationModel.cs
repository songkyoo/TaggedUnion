namespace Macaron.Union;

internal sealed record UnionCaseGenerationModel(
    UnionCaseStorageKind StorageKind,
    bool SupportsConversionOperators,
    string FullyQualifiedTypeName,
    string ParamName,
    byte Tag
);
