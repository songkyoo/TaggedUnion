using Microsoft.CodeAnalysis;

using static Microsoft.CodeAnalysis.SpecialType;
using static Microsoft.CodeAnalysis.TypeKind;

namespace Macaron.Union;

internal static class OfficialUnionSupport
{
    #region Constants
    private const string AttributeMetadataName = "System.Attribute";
    private const string UnionAttributeMetadataName = "System.Runtime.CompilerServices.UnionAttribute";
    private const string UnionInterfaceMetadataName = "System.Runtime.CompilerServices.IUnion";
    #endregion

    #region Static Methods
    public static bool IsAvailable(Compilation compilation)
    {
        var unionAttributeSymbol = compilation.GetTypeByMetadataName(UnionAttributeMetadataName);
        var unionInterfaceSymbol = compilation.GetTypeByMetadataName(UnionInterfaceMetadataName);

        return IsUnionAttribute(unionAttributeSymbol, compilation)
            && IsUnionInterface(unionInterfaceSymbol, compilation);
    }

    private static bool IsUnionAttribute(INamedTypeSymbol? symbol, Compilation compilation)
    {
        if (symbol is not { TypeKind: Class, Arity: 0 })
        {
            return false;
        }

        var attributeSymbol = compilation.GetTypeByMetadataName(AttributeMetadataName);
        var currentSymbol = symbol;

        while (currentSymbol != null)
        {
            if (SymbolEqualityComparer.Default.Equals(currentSymbol, attributeSymbol))
            {
                return true;
            }

            currentSymbol = currentSymbol.BaseType;
        }

        return false;
    }

    private static bool IsUnionInterface(INamedTypeSymbol? symbol, Compilation compilation)
    {
        if (symbol is not { TypeKind: Interface, Arity: 0 })
        {
            return false;
        }

        var objectSymbol = compilation.GetSpecialType(System_Object);

        return symbol
            .GetMembers("Value")
            .OfType<IPropertySymbol>()
            .Any(propertySymbol =>
                propertySymbol.Parameters.Length == 0
                && propertySymbol.GetMethod is { DeclaredAccessibility: Accessibility.Public }
                && SymbolEqualityComparer.Default.Equals(propertySymbol.Type, objectSymbol)
            );
    }
    #endregion
}
