namespace Macaron.Union;

internal static class UnionCaseGenerationModelHelper
{
    public static string GetValuePropertyName(byte tag)
    {
        return $"__{tag}";
    }
}
