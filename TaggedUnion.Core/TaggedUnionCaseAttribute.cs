using System.Diagnostics;

namespace Macaron.Union;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(validOn: AttributeTargets.Struct, AllowMultiple = true)]
public sealed class TaggedUnionCaseAttribute : Attribute
{
    #region Properties
    public Type Type { get; }

    public string? ParamName { get; }

    public byte Tag { get; }
    #endregion

    #region Constructors
    public TaggedUnionCaseAttribute(Type type, string? paramName = null, byte tag = 0)
    {
        Type = type;
        ParamName = paramName;
        Tag = tag;
    }
    #endregion
}
