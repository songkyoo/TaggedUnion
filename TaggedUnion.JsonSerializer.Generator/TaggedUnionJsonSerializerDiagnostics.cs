using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal static class TaggedUnionJsonSerializerDiagnostics
{
    public static readonly DiagnosticDescriptor TaggedUnionAttributeRequiredRule = new(
        id: "MTUJS0001",
        title: "TaggedUnionJsonSerializer requires TaggedUnion",
        messageFormat: "Target type '{0}' with a TaggedUnionJsonSerializer attribute must also have a TaggedUnion attribute.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
