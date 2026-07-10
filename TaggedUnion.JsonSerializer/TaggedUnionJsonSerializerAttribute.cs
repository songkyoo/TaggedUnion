using System.Diagnostics;

namespace Macaron.Union;

[Conditional("SOURCE_GENERATOR_ONLY")]
[AttributeUsage(validOn: AttributeTargets.Struct)]
public sealed class TaggedUnionJsonSerializerAttribute : Attribute
{
}
