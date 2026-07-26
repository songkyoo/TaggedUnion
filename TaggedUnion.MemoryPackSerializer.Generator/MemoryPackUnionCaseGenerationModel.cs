namespace Macaron.Union;

internal sealed record MemoryPackUnionCaseGenerationModel(
    UnionCaseGenerationModel UnionCase,
    bool IsMemoryPackable
)
{
    public bool IsReferenceType => UnionCase.IsReferenceType;

    public string FullyQualifiedTypeName => UnionCase.FullyQualifiedTypeName;

    public byte Tag => UnionCase.Tag;
}
