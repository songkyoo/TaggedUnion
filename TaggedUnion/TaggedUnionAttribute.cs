using System.Diagnostics;

namespace Macaron.Union;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(validOn: AttributeTargets.Struct)]
public sealed class TaggedUnionAttribute : Attribute
{
    #region Properties
    public IReadOnlyList<Type> Types { get; }
    #endregion

    #region Constructors
    public TaggedUnionAttribute(Type type1, Type type2)
    {
        Types = [type1, type2];
    }

    public TaggedUnionAttribute(Type type1, Type type2, Type type3)
    {
        Types = [type1, type2, type3];
    }

    public TaggedUnionAttribute(Type type1, Type type2, Type type3, Type type4)
    {
        Types = [type1, type2, type3, type4];
    }

    public TaggedUnionAttribute(Type type1, Type type2, Type type3, Type type4, Type type5)
    {
        Types = [type1, type2, type3, type4, type5];
    }

    public TaggedUnionAttribute(Type type1, Type type2, Type type3, Type type4, Type type5, Type type6)
    {
        Types = [type1, type2, type3, type4, type5, type6];
    }

    public TaggedUnionAttribute(Type type1, Type type2, Type type3, Type type4, Type type5, Type type6, Type type7)
    {
        Types = [type1, type2, type3, type4, type5, type6, type7];
    }

    public TaggedUnionAttribute(Type type1, Type type2, Type type3, Type type4, Type type5, Type type6, Type type7, Type type8)
    {
        Types = [type1, type2, type3, type4, type5, type6, type7, type8];
    }
    #endregion
}
