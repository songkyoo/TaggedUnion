using static Microsoft.CodeAnalysis.CSharp.SyntaxFacts;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Macaron.TaggedUnion;

public static class StringHelper
{
    public static string GetCamelCaseName(string name)
    {
        return name.Length > 0 && char.IsLetter(name[0])
            ? char.ToLowerInvariant(name[0]) + (name.Length > 1 ? name[1..] : "")
            : name;
    }

    public static string EscapeIdentifier(string name)
    {
        return GetKeywordKind(name) != None || GetContextualKeywordKind(name) != None
            ? $"@{name}"
            : name;
    }
}
