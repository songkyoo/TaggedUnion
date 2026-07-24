using Microsoft.CodeAnalysis;

namespace Macaron.Union;

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

    public static readonly DiagnosticDescriptor DuplicateCaseTypeRule = new(
        id: "MTU0005",
        title: "Case type cannot be duplicated",
        messageFormat: "Case type '{0}' is specified more than once in tagged union target type '{1}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateCaseParameterNameRule = new(
        id: "MTU0006",
        title: "Case parameter name cannot be duplicated",
        messageFormat: "Case type '{0}' generates duplicate parameter name '{1}' in tagged union target type '{2}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidCaseParameterNameRule = new(
        id: "MTU0007",
        title: "Case parameter name must be valid",
        messageFormat: "Case type '{0}' specifies invalid parameter name '{1}' in tagged union target type '{2}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CaseAttributeTypeMustMatchCaseTypeRule = new(
        id: "MTU0008",
        title: "TaggedUnionCase type must match a case type",
        messageFormat: "TaggedUnionCase type '{0}' is not specified in tagged union target type '{1}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor InvalidCaseTagRule = new(
        id: "MTU0009",
        title: "Case tag must be greater than zero",
        messageFormat: "Case type '{0}' specifies invalid tag '{1}' in tagged union target type '{2}'. Tag 0 is reserved for an uninitialized value.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor DuplicateCaseTagRule = new(
        id: "MTU0010",
        title: "Case tag cannot be duplicated",
        messageFormat: "Case type '{0}' specifies duplicate tag '{1}' in tagged union target type '{2}'.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CaseAttributeRequiresTaggedUnionAttributeRule = new(
        id: "MTU0011",
        title: "TaggedUnionCase requires TaggedUnion",
        messageFormat: "Target type '{0}' with a TaggedUnionCase attribute must also have a TaggedUnion attribute.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static Diagnostic CreateDiagnostic(UnionAnalysisError error)
    {
        return error switch
        {
            UnionAnalysisError.TargetTypeMustBeReadOnlyPartialStruct value => Diagnostic.Create(
                descriptor: TargetTypeMustBeReadOnlyPartialStructRule,
                location: value.Location,
                messageArgs: [value.TargetTypeName]
            ),
            UnionAnalysisError.TargetTypeMustBeNonGeneric value => Diagnostic.Create(
                descriptor: TargetTypeMustBeNonGenericRule,
                location: value.Location,
                messageArgs: [value.TargetTypeName]
            ),
            UnionAnalysisError.TargetTypeCannotDeclareInstanceConstructors value => Diagnostic.Create(
                descriptor: TargetTypeCannotDeclareInstanceConstructorsRule,
                location: value.Location,
                messageArgs: [value.TargetTypeName]
            ),
            UnionAnalysisError.UnsupportedCaseType value => Diagnostic.Create(
                descriptor: UnsupportedCaseTypeRule,
                location: value.Location,
                messageArgs: [value.CaseTypeName, value.TargetTypeName]
            ),
            UnionAnalysisError.DuplicateCaseType value => Diagnostic.Create(
                descriptor: DuplicateCaseTypeRule,
                location: value.Location,
                messageArgs: [value.CaseTypeName, value.TargetTypeName]
            ),
            UnionAnalysisError.DuplicateCaseParameterName value => Diagnostic.Create(
                descriptor: DuplicateCaseParameterNameRule,
                location: value.Location,
                messageArgs: [value.CaseTypeName, value.ParameterName, value.TargetTypeName]
            ),
            UnionAnalysisError.InvalidCaseParameterName value => Diagnostic.Create(
                descriptor: InvalidCaseParameterNameRule,
                location: value.Location,
                messageArgs: [value.CaseTypeName, value.ParameterName, value.TargetTypeName]
            ),
            UnionAnalysisError.CaseAttributeTypeMustMatchCaseType value => Diagnostic.Create(
                descriptor: CaseAttributeTypeMustMatchCaseTypeRule,
                location: value.Location,
                messageArgs: [value.CaseTypeName, value.TargetTypeName]
            ),
            UnionAnalysisError.InvalidCaseTag value => Diagnostic.Create(
                descriptor: InvalidCaseTagRule,
                location: value.Location,
                messageArgs: [value.CaseTypeName, value.Tag, value.TargetTypeName]
            ),
            UnionAnalysisError.DuplicateCaseTag value => Diagnostic.Create(
                descriptor: DuplicateCaseTagRule,
                location: value.Location,
                messageArgs: [value.CaseTypeName, value.Tag, value.TargetTypeName]
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(error)),
        };
    }
}
