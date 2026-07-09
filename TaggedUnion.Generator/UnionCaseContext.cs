namespace Macaron.Union;

internal sealed record UnionCaseContext(
    UnionCaseStorageKind StorageKind,
    string FullyQualifiedTypeName,
    string ParamName,
    int Tag
);
