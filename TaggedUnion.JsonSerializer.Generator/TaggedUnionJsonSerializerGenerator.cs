using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

using static Macaron.Union.HintNameHelper;
using static Macaron.Union.TaggedUnionMetadataNames;

namespace Macaron.Union;

[Generator(LanguageNames.CSharp)]
public sealed class TaggedUnionJsonSerializerGenerator : IIncrementalGenerator
{
    #region Constants
    private const string TaggedUnionJsonSerializerAttributeMetadataName = "Macaron.Union.TaggedUnionJsonSerializerAttribute";
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
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == TaggedUnionAttribute);

        return taggedUnionAttribute != null
            ? UnionContextFactory.Create(context, taggedUnionAttribute, cancellationToken)
            : null;
    }

    private static SourceText GenerateSourceText(UnionContext context)
    {
        var writer = new TaggedUnionJsonSerializerSourceWriter(context);
        var source = writer.Generate();

        return SourceText.From(source, Encoding.UTF8);
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

        context.RegisterSourceOutput(results, static (sourceProductionContext, result) =>
        {
            if (result is not UnionValidationResult.Success { Context: var context })
            {
                return;
            }

            var hintName = $"{GetTypeHintName(context.TypeSymbol)}.JsonSerializer.g.cs";
            var sourceText = GenerateSourceText(context);

            sourceProductionContext.AddSource(hintName, sourceText);
        });
    }
    #endregion
}
