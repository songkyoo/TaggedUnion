using static Macaron.Union.UnionCaseStorageKind;

namespace Macaron.Union;

internal static class UnionCaseStorageHelper
{
    public static UnionCaseSourceGenerationModel CreateSourceGenerationModel(
        UnionCaseGenerationModel model
    )
    {
        var storageKind = model switch
        {
            { IsReferenceType: true } => Reference,
            { IsUnmanagedType: true } => Unmanaged,
            _ => Managed,
        };

        return new UnionCaseSourceGenerationModel(
            StorageKind: storageKind,
            SupportsConversionOperators: model.SupportsConversionOperators,
            FullyQualifiedTypeName: model.FullyQualifiedTypeName,
            ParamName: model.ParamName,
            Tag: model.Tag
        );
    }

    public static string GetValueAccessorString(UnionCaseSourceGenerationModel model)
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
