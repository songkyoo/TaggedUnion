using static Macaron.Union.UnionCaseStorageKind;

namespace Macaron.Union;

internal static class UnionCaseGenerationModelHelper
{
    public static string GetValueAccessorString(UnionCaseGenerationModel model)
    {
        return model.StorageKind switch
        {
            Reference => "_reference",
            Unmanaged => $"_unmanaged.Value{model.Tag}",
            Managed => $"_value{model.Tag}",
            _ => throw new InvalidOperationException($"Invalid storage kind: {model.StorageKind}")
        };
    }
}
