using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal static class ConversionOperatorGenerationPolicy
{
    #region Constants
    private const string GenerateConversionOperatorsProperty = "GenerateConversionOperators";
    #endregion

    #region Static Methods
    public static bool IsEnabled(AttributeData taggedUnionAttribute)
    {
        return taggedUnionAttribute.NamedArguments.Any(x => x is
        {
            Key: GenerateConversionOperatorsProperty,
            Value.Value: true,
        });
    }

    public static bool SupportsCase(UnionCaseGenerationModel model, bool generateConversionOperators)
    {
        return generateConversionOperators && !model.IsInterfaceType;
    }
    #endregion
}
