using Microsoft.CodeAnalysis;

namespace Macaron.TaggedUnion;

internal sealed record ConstructorTypeArgumentContext(
    SyntaxNode Node,
    ITypeSymbol Symbol
);
