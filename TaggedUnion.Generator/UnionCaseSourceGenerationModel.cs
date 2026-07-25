namespace Macaron.Union;

internal sealed record UnionCaseSourceGenerationModel(
    UnionCaseStorageKind StorageKind,
    bool IsInterfaceType,
    bool SupportsConversionOperators,
    string FullyQualifiedTypeName,
    string ParamName,
    byte Tag
);
