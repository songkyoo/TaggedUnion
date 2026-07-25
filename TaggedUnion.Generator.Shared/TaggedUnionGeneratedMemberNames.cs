namespace Macaron.Union;

internal static class TaggedUnionGeneratedMemberNames
{
    public const string TagField = "_tag";

    public static string GetValueProperty(byte tag)
    {
        return $"__{tag}";
    }
}
