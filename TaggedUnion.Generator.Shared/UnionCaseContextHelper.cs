using static Macaron.Union.UnionCaseStorageKind;

namespace Macaron.Union;

internal static class UnionCaseContextHelper
{
    public static string GetValueAccessorString(UnionCaseContext context)
    {
        return context.StorageKind switch
        {
            Reference => "_reference",
            Unmanaged => $"_unmanaged.Value{context.Tag}",
            Managed => $"_value{context.Tag}",
            _ => throw new InvalidOperationException($"Invalid storage kind: {context.StorageKind}")
        };
    }
}
