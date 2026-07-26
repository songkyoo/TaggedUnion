using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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

        public sealed record Failure(
            ImmutableArray<Diagnostic> Diagnostics
        ) : MemoryPackSerializerAnalysisResult;
    }
    #endregion

    #region Static Methods
    private static MemoryPackSerializerAnalysisResult? CreateAnalysisResult(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.TargetSymbol is not INamedTypeSymbol targetTypeSymbol)
        {
            return null;
        }

        var taggedUnionAttribute = context
            .TargetSymbol
            .GetAttributes()
            .FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == TaggedUnionAttribute);

        if (taggedUnionAttribute == null)
        {
            var location = context
                .Attributes[0]
                .ApplicationSyntaxReference?
                .GetSyntax(cancellationToken)
                .GetLocation()
                ?? context.TargetNode.GetLocation();
            var diagnostic = Diagnostic.Create(
                descriptor: TaggedUnionMemoryPackSerializerDiagnostics.TaggedUnionAttributeRequiredRule,
                location,
                messageArgs: [targetTypeSymbol.Name]
            );

            return new MemoryPackSerializerAnalysisResult.Failure(ImmutableArray.Create(diagnostic));
        }

        var result = UnionGenerationModelFactory.Create(
            context,
            taggedUnionAttribute,
            cancellationToken
        );

        if (result is not AnalysisResult.Success success)
        {
            return null;
        }

        var diagnostics = CreateTargetDiagnostics(
            targetTypeSymbol,
            context.SemanticModel.Compilation,
            cancellationToken
        );

        if (diagnostics.Length > 0)
        {
            return new MemoryPackSerializerAnalysisResult.Failure(diagnostics);
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

    private static ImmutableArray<Diagnostic> CreateTargetDiagnostics(
        INamedTypeSymbol targetTypeSymbol,
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var diagnosticsBuilder = ImmutableArray.CreateBuilder<Diagnostic>();
        var memoryPackableAttribute = GetMemoryPackableAttribute(targetTypeSymbol);

        if (memoryPackableAttribute != null)
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionMemoryPackSerializerDiagnostics.ExistingMemoryPackSerializationNotSupportedRule,
                location: memoryPackableAttribute
                    .ApplicationSyntaxReference?
                    .GetSyntax(cancellationToken)
                    .GetLocation()
                    ?? targetTypeSymbol.Locations.FirstOrDefault()
                    ?? Location.None,
                messageArgs: [targetTypeSymbol.Name]
            ));
        }
        else if (ImplementsSelfMemoryPackable(targetTypeSymbol, compilation))
        {
            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionMemoryPackSerializerDiagnostics.ExistingMemoryPackSerializationNotSupportedRule,
                location: GetMemoryPackableInterfaceLocation(
                    targetTypeSymbol,
                    compilation,
                    cancellationToken
                ),
                messageArgs: [targetTypeSymbol.Name]
            ));
        }

        foreach (var staticConstructor in targetTypeSymbol
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Where(methodSymbol => methodSymbol is
            {
                MethodKind: MethodKind.StaticConstructor,
                IsImplicitlyDeclared: false,
            })
        )
        {
            var location = staticConstructor
                .DeclaringSyntaxReferences
                .Select(syntaxReference => syntaxReference.GetSyntax(cancellationToken) as ConstructorDeclarationSyntax)
                .FirstOrDefault(syntax => syntax != null)?
                .Identifier
                .GetLocation()
                ?? targetTypeSymbol.Locations.FirstOrDefault()
                ?? Location.None;

            diagnosticsBuilder.Add(Diagnostic.Create(
                descriptor: TaggedUnionMemoryPackSerializerDiagnostics.StaticConstructorNotSupportedRule,
                location,
                messageArgs: [targetTypeSymbol.Name]
            ));
        }

        return diagnosticsBuilder.ToImmutable();
    }

    private static SourceText GenerateSourceText(TaggedUnionMemoryPackSerializerGenerationModel model)
    {
        var writer = new TaggedUnionMemoryPackSerializerSourceWriter(model);
        var source = writer.Generate();

        return SourceText.From(text: source, encoding: Encoding.UTF8);
    }

    private static AttributeData? GetMemoryPackableAttribute(ITypeSymbol typeSymbol)
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

    private static Location GetMemoryPackableInterfaceLocation(
        INamedTypeSymbol targetTypeSymbol,
        Compilation compilation,
        CancellationToken cancellationToken
    )
    {
        var memoryPackableInterface = compilation.GetTypeByMetadataName(
            MemoryPackableInterfaceMetadataName
        );

        foreach (var syntaxReference in targetTypeSymbol.DeclaringSyntaxReferences)
        {
            if (syntaxReference.GetSyntax(cancellationToken) is not TypeDeclarationSyntax
            {
                BaseList: { } baseList,
            })
            {
                continue;
            }

            var semanticModel = compilation.GetSemanticModel(baseList.SyntaxTree);

            foreach (var baseType in baseList.Types)
            {
                if (semanticModel.GetTypeInfo(baseType.Type, cancellationToken).Type is
                    not INamedTypeSymbol interfaceSymbol
                    || !SymbolEqualityComparer.Default.Equals(
                        interfaceSymbol.OriginalDefinition,
                        memoryPackableInterface
                    )
                    || interfaceSymbol.TypeArguments.Length != 1
                    || !SymbolEqualityComparer.Default.Equals(
                        interfaceSymbol.TypeArguments[0],
                        targetTypeSymbol
                    )
                )
                {
                    continue;
                }

                return baseType.Type.GetLocation();
            }
        }

        return targetTypeSymbol.Locations.FirstOrDefault() ?? Location.None;
    }

    private static bool ImplementsSelfMemoryPackable(ITypeSymbol typeSymbol, Compilation compilation)
    {
        var memoryPackableInterface = compilation.GetTypeByMetadataName(
            MemoryPackableInterfaceMetadataName
        );

        return memoryPackableInterface != null && typeSymbol.AllInterfaces.Any(interfaceSymbol =>
            SymbolEqualityComparer.Default.Equals(interfaceSymbol.OriginalDefinition, memoryPackableInterface)
            && interfaceSymbol.TypeArguments.Length == 1
            && SymbolEqualityComparer.Default.Equals(interfaceSymbol.TypeArguments[0], typeSymbol)
        );
    }

    private static bool IsMemoryPackable(ITypeSymbol typeSymbol, Compilation compilation)
    {
        if (ImplementsSelfMemoryPackable(typeSymbol, compilation))
        {
            return true;
        }

        var memoryPackableAttribute = GetMemoryPackableAttribute(typeSymbol);

        return memoryPackableAttribute != null && !IsNoGenerate(memoryPackableAttribute, compilation);
    }

    private static bool IsMemoryPackableAttribute(AttributeData attribute)
    {
        return attribute.AttributeClass?.ToDisplayString() == MemoryPackableAttributeMetadataName;
    }

    private static bool IsNoGenerate(AttributeData attribute, Compilation compilation)
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
            source: analysisResultProvider
                .Where(static result => result is MemoryPackSerializerAnalysisResult.Failure)
                .Select(static (result, _) => ((MemoryPackSerializerAnalysisResult.Failure)result!).Diagnostics),
            action: static (sourceProductionContext, diagnostics) =>
            {
                foreach (var diagnostic in diagnostics)
                {
                    sourceProductionContext.ReportDiagnostic(diagnostic);
                }
            }
        );
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
