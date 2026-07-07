using Microsoft.CodeAnalysis;

namespace Macaron.TaggedUnion;

internal sealed record UnionCaseContext(
    ITypeSymbol TypeSymbol,
    UnionCaseStorageKind StorageKind,
    int Tag,
    string FullyQualifiedTypeName,
    string ParamName
);
