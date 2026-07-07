using Microsoft.CodeAnalysis;

namespace Macaron.TaggedUnion;

public sealed record ConstructorTypeArgumentContext(
    SyntaxNode Node,
    ITypeSymbol Symbol
);
