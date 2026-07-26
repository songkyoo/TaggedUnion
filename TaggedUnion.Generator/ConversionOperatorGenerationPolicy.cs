using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

    public static Location GetOptionLocation(
        AttributeData taggedUnionAttribute,
        CancellationToken cancellationToken
    )
    {
        var attributeSyntax = taggedUnionAttribute
            .ApplicationSyntaxReference?
            .GetSyntax(cancellationToken) as AttributeSyntax;
        var argument = attributeSyntax?
            .ArgumentList?
            .Arguments
            .FirstOrDefault(argument =>
            {
                return argument.NameEquals?.Name.Identifier.ValueText == GenerateConversionOperatorsProperty;
            });

        return argument?.Expression.GetLocation()
            ?? attributeSyntax?.GetLocation()
            ?? Location.None;
    }
    #endregion
}
