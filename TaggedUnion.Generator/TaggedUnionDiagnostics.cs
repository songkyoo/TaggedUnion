using Microsoft.CodeAnalysis;

namespace Macaron.TaggedUnion;

internal static class TaggedUnionDiagnostics
{
    public static readonly DiagnosticDescriptor TargetTypeMustBeReadOnlyPartialStructRule = new(
        id: "MTU0001",
        title: "Target type must be a readonly partial struct",
        messageFormat: "Tagged union target type '{0}' must be declared as a readonly partial struct.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TargetTypeMustBeNonGenericRule = new(
        id: "MTU0002",
        title: "Target type must be non-generic",
        messageFormat: "Tagged union target type '{0}' cannot be generic.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor TargetTypeCannotDeclareInstanceConstructorsRule = new(
        id: "MTU0003",
        title: "Target type cannot declare instance constructors",
        messageFormat: "Tagged union target type '{0}' cannot declare instance constructors.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnsupportedCaseTypeRule = new(
        id: "MTU0004",
        title: "Case type is not supported",
        messageFormat: "Case type '{0}' cannot be used in tagged union target type '{1}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
