using Microsoft.CodeAnalysis;

namespace Macaron.Union;

internal static class TaggedUnionMemoryPackSerializerDiagnostics
{
    public static readonly DiagnosticDescriptor TaggedUnionAttributeRequiredRule = new(
        id: "MTUMP0001",
        title: "TaggedUnionMemoryPackSerializer requires TaggedUnion",
        messageFormat: "Target type '{0}' with a TaggedUnionMemoryPackSerializer attribute must also have a TaggedUnion attribute.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ExistingMemoryPackSerializationNotSupportedRule = new(
        id: "MTUMP0002",
        title: "TaggedUnionMemoryPackSerializer conflicts with existing MemoryPack serialization",
        messageFormat: "Target type '{0}' with a TaggedUnionMemoryPackSerializer attribute cannot also have a MemoryPackable attribute or implement IMemoryPackable<T>.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor StaticConstructorNotSupportedRule = new(
        id: "MTUMP0003",
        title: "TaggedUnionMemoryPackSerializer does not support a user-defined static constructor",
        messageFormat: "Target type '{0}' with a TaggedUnionMemoryPackSerializer attribute cannot declare a static constructor. Implement 'static partial void StaticConstructor()' instead.",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
