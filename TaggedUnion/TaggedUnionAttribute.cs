using System.Diagnostics;

namespace Macaron.Union;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(validOn: AttributeTargets.Struct)]
public sealed class TaggedUnionAttribute : Attribute
{
    #region Properties
    public string StateParamName { get; set; } = "state";

    public bool GenerateConversionOperators { get; set; }
    #endregion
}
