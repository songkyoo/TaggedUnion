namespace Macaron.Union;

internal sealed record UnionCaseGenerationModel(
    UnionCaseStorageKind StorageKind,
    string FullyQualifiedTypeName,
    string ParamName,
    byte Tag
);
