using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace Macaron.Union;

internal static class TypeDeclarationHelper
{
    public static ImmutableArray<INamedTypeSymbol> GetContainingTypes(INamedTypeSymbol typeSymbol)
    {
        var containingTypes = new List<INamedTypeSymbol>();
        var parentType = typeSymbol.ContainingType;

        while (parentType != null)
        {
            containingTypes.Add(parentType);
            parentType = parentType.ContainingType;
        }

        containingTypes.Reverse();

        return containingTypes.ToImmutableArray();
    }

    public static string GetPartialTypeDeclarationString(INamedTypeSymbol typeSymbol)
    {
        var typeKind = GetTypeKindString(typeSymbol);
        var typeName = typeSymbol.ToDisplayString(MinimallyQualifiedFormat);

        return $"partial {typeKind} {typeName}";

        #region Local Functions
        static string GetTypeKindString(INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.IsRecord)
            {
                return typeSymbol.TypeKind is TypeKind.Struct ? "record struct" : "record";
            }

            return typeSymbol.TypeKind switch
            {
                TypeKind.Class => "class",
                TypeKind.Struct => "struct",
                TypeKind.Interface => "interface",
                _ => throw new InvalidOperationException($"Invalid type kind: {typeSymbol.TypeKind}")
            };
        }
        #endregion
    }
}
