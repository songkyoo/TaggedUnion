using Microsoft.CodeAnalysis;

namespace Macaron.TaggedUnion;

internal static class TaggedUnionDiagnostics
{
    public static readonly DiagnosticDescriptor TargetTypeMustBeReadOnlyPartialStructRule = new(
        id: "MTU0001",
        title: "Target type must be read-only partial struct",
        messageFormat: "Type '{0}' is not a read-only partial struct. Only read-only partial structs can be used as tagged union targets.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
