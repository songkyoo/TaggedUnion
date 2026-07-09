namespace Macaron.Union;

internal sealed record UnionCaseContext(
    UnionCaseStorageKind StorageKind,
    int Tag,
    string FullyQualifiedTypeName,
    string ParamName
);
