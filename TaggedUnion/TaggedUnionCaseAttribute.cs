using System.Diagnostics;

namespace Macaron.Union;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(validOn: AttributeTargets.Struct, AllowMultiple = true)]
public sealed class TaggedUnionCaseAttribute(Type type, string? paramName = null, byte tag = 0) : Attribute
{
    #region Properties
    public Type Type { get; } = type;

    public string? ParamName { get; } = paramName;

    public byte Tag { get; } = tag;
    #endregion
}
