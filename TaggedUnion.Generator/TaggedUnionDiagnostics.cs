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

    public static readonly DiagnosticDescriptor TargetTypeMustBeNotGenericRule = new(
        id: "MTU0002",
        title: "Target type must be not generic",
        messageFormat: "Type '{0}' is a generic type. Only non-generic structs can be used as tagged union targets.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UserDefinedConstructorNotAllowedRule = new(
        id: "MTU0003",
        title: "User-defined constructor not allowed",
        messageFormat: "User-defined constructor is not allowed in tagged union target type '{0}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NotAllowedCaseTypeRule = new(
        id: "MTU0004",
        title: "Object case type not allowed",
        messageFormat: "Case type '{0}' is not allowed in tagged union target type '{1}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
