using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.Union;

internal sealed record UnionCaseCandidateContext(
    AttributeArgumentSyntax ArgumentSyntax,
    ITypeSymbol TypeSymbol,
    string ParamName
);
