using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.TaggedUnion;

internal sealed record UnionCaseCandidateContext(
    AttributeArgumentSyntax ArgumentSyntax,
    ITypeSymbol TypeSymbol,
    string ParamName
);
