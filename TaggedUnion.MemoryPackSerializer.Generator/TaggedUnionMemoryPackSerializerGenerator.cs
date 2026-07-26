using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

using static Macaron.Union.TaggedUnionMetadataNames;
using static Macaron.Union.TypeDeclarationHelper;

namespace Macaron.Union;

[Generator]
public sealed class TaggedUnionMemoryPackSerializerGenerator : IIncrementalGenerator
{
    #region Constants
    private const string MemoryPackableAttributeMetadataName = "MemoryPack.MemoryPackableAttribute";
    private const string MemoryPackableInterfaceMetadataName = "MemoryPack.IMemoryPackable`1";
    private const string MemoryPackGenerateTypeMetadataName = "MemoryPack.GenerateType";
    private const string TaggedUnionMemoryPackSerializerAttributeMetadataName = "Macaron.Union.TaggedUnionMemoryPackSerializerAttribute";
    #endregion

    #region Types
    private abstract record MemoryPackSerializerAnalysisResult
    {
        public sealed record Success(
            TaggedUnionMemoryPackSerializerGenerationModel Model
        ) : MemoryPackSerializerAnalysisResult;
    }
    #endregion

    #region Static Methods
    private static MemoryPackSerializerAnalysisResult? CreateAnalysisResult(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        var taggedUnionAttribute = context
            .TargetSymbol
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == TaggedUnionAttribute);

        if (taggedUnionAttribute == null)
        {
            return null;
        }

        var result = UnionGenerationModelFactory.Create(
            context,
            taggedUnionAttribute,
            cancellationToken
        );

        if (result is not AnalysisResult.Success success
            || context.TargetSymbol is not INamedTypeSymbol targetTypeSymbol
        )
        {
            return null;
        }

        var model = CreateGenerationModel(
            success.Model,
            targetTypeSymbol,
            context.SemanticModel.Compilation
        );

        return model != null
            ? new MemoryPackSerializerAnalysisResult.Success(model)
            : null;
    }

    private static TaggedUnionMemoryPackSerializerGenerationModel? CreateGenerationModel(
        UnionGenerationModel unionModel,
        INamedTypeSymbol targetTypeSymbol,
        Compilation compilation
    )
    {
        var casesInterfaceDefinition = compilation.GetTypeByMetadataName(
            $"{TaggedUnionCasesInterface}`{unionModel.Cases.Length}"
        );
        var casesInterface = targetTypeSymbol.Interfaces.FirstOrDefault(interfaceSymbol =>
        {
            return SymbolEqualityComparer.Default.Equals(interfaceSymbol.OriginalDefinition, casesInterfaceDefinition);
        });

        if (casesInterface == null
            || casesInterface.TypeArguments.Length != unionModel.Cases.Length
        )
        {
            return null;
        }

        var casesBuilder = ImmutableArray.CreateBuilder<MemoryPackUnionCaseGenerationModel>(
            initialCapacity: unionModel.Cases.Length
        );

        for (var i = 0; i < unionModel.Cases.Length; i++)
        {
            casesBuilder.Add(new MemoryPackUnionCaseGenerationModel(
                UnionCase: unionModel.Cases[i],
                IsMemoryPackable: IsMemoryPackable(casesInterface.TypeArguments[i], compilation)
            ));
        }

        return new TaggedUnionMemoryPackSerializerGenerationModel(
            Union: unionModel,
            Cases: casesBuilder.ToImmutable()
        );
    }

    private static SourceText GenerateSourceText(TaggedUnionMemoryPackSerializerGenerationModel model)
    {
        var writer = new TaggedUnionMemoryPackSerializerSourceWriter(model);
        var source = writer.Generate();

        return SourceText.From(text: source, encoding: Encoding.UTF8);
    }

    private static bool IsMemoryPackable(ITypeSymbol typeSymbol, Compilation compilation)
    {
        var memoryPackableInterface = compilation.GetTypeByMetadataName(
            MemoryPackableInterfaceMetadataName
        );

        if (memoryPackableInterface != null
            && typeSymbol.AllInterfaces.Any(interfaceSymbol =>
                SymbolEqualityComparer.Default.Equals(interfaceSymbol.OriginalDefinition, memoryPackableInterface)
                && interfaceSymbol.TypeArguments.Length == 1
                && SymbolEqualityComparer.Default.Equals(interfaceSymbol.TypeArguments[0], typeSymbol)
            )
        )
        {
            return true;
        }

        var memoryPackableAttribute = GetMemoryPackableAttribute(typeSymbol);

        return memoryPackableAttribute != null && !IsNoGenerate(memoryPackableAttribute, compilation);

        #region Local Functions
        static AttributeData? GetMemoryPackableAttribute(ITypeSymbol typeSymbol)
        {
            var attribute = typeSymbol.GetAttributes().FirstOrDefault(IsMemoryPackableAttribute);

            if (attribute != null)
            {
                return attribute;
            }

            return typeSymbol is INamedTypeSymbol namedTypeSymbol
                ? namedTypeSymbol.OriginalDefinition.GetAttributes().FirstOrDefault(IsMemoryPackableAttribute)
                : null;
        }

        static bool IsMemoryPackableAttribute(AttributeData attribute)
        {
            return attribute.AttributeClass?.ToDisplayString() == MemoryPackableAttributeMetadataName;
        }

        static bool IsNoGenerate(AttributeData attribute, Compilation compilation)
        {
            if (attribute.AttributeConstructor is not { Parameters.Length: > 0 } constructor
                || attribute.ConstructorArguments.Length == 0
            )
            {
                return false;
            }

            var generateTypeSymbol = compilation.GetTypeByMetadataName(MemoryPackGenerateTypeMetadataName);

            if (generateTypeSymbol == null
                || !SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, generateTypeSymbol)
            )
            {
                return false;
            }

            var noGenerateValue = generateTypeSymbol
                .GetMembers("NoGenerate")
                .OfType<IFieldSymbol>()
                .FirstOrDefault()?
                .ConstantValue;

            return noGenerateValue != null && Equals(attribute.ConstructorArguments[0].Value, noGenerateValue);
        }
        #endregion
    }
    #endregion

    #region IIncrementalGenerator Interface
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var analysisResultProvider = context
            .SyntaxProvider
            .ForAttributeWithMetadataName(
                fullyQualifiedMetadataName: TaggedUnionMemoryPackSerializerAttributeMetadataName,
                predicate: static (syntaxNode, _) => IsStructTypeDeclarationSyntax(syntaxNode),
                transform: CreateAnalysisResult
            )
            .WithTrackingName(nameof(AnalysisResult));
        var generationModelProvider = analysisResultProvider
            .Where(static result => result is MemoryPackSerializerAnalysisResult.Success)
            .Select(static (result, _) => ((MemoryPackSerializerAnalysisResult.Success)result!).Model)
            .WithTrackingName(nameof(UnionGenerationModel));

        context.RegisterSourceOutput(
            source: generationModelProvider,
            action: static (sourceProductionContext, model) =>
            {
                var hintName = $"{model.HintName}.MemoryPackSerializer.g.cs";
                var sourceText = GenerateSourceText(model);

                sourceProductionContext.AddSource(hintName, sourceText);
            }
        );
    }
    #endregion
}
