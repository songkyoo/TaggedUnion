using static System.StringComparison;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFacts;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Macaron.Union;

internal static class StringHelper
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

    public static bool IsValidParameterName(string name)
    {
        var identifier = name.StartsWith("@", Ordinal)
            ? name[1..]
            : name;

        if (identifier.Length == 0 || !IsIdentifierStartCharacter(identifier[0]))
        {
            return false;
        }

        for (var i = 1; i < identifier.Length; i++)
        {
            if (!IsIdentifierPartCharacter(identifier[i]))
            {
                return false;
            }
        }

        return true;
    }
}
