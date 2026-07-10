using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Macaron.Union;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionJsonSerializerGenerator : IIncrementalGenerator
{
    #region Constants
    private const string TaggedUnionJsonSerializerAttributeMetadataName =
        "Macaron.Union.TaggedUnionJsonSerializerAttribute";
    #endregion

    #region Static Methods
    private static UnionValidationResult? CreateUnionContext(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var taggedUnionAttribute = context
            .TargetSymbol
            .GetAttributes()
            .FirstOrDefault(attribute =>
                attribute.AttributeClass?.ToDisplayString()
                    == TaggedUnionMetadataNames.TaggedUnionAttribute
            );

        return taggedUnionAttribute != null
            ? UnionContextFactory.Create(context, taggedUnionAttribute, cancellationToken)
            : null;
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var results = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: TaggedUnionJsonSerializerAttributeMetadataName,
                predicate: static (syntaxNode, _) => syntaxNode is StructDeclarationSyntax,
                transform: CreateUnionContext
            )
            .Where(static result => result != null)
            .Select(static (result, _) => result!);

        context.RegisterSourceOutput(results, static (_, result) =>
        {
            if (result is not UnionValidationResult.Success)
            {
                return;
            }
        });
    }
    #endregion
}
