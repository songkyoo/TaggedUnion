using System.Collections.Immutable;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Macaron.Union.Tests;

internal static partial class Helper
{
    private const string AttributeMetadataName = "System.Attribute";
    private const string UnionAttributeMetadataName = "System.Runtime.CompilerServices.UnionAttribute";
    private const string UnionInterfaceMetadataName = "System.Runtime.CompilerServices.IUnion";

    private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default.WithLanguageVersion(
        LanguageVersion.Preview
    );

    [GeneratedRegex(
        pattern: @"^(?<indent>[ \t]*)partial struct (?<typeName>@?[A-Za-z_][A-Za-z0-9_]*) : global::System\.IEquatable<\k<typeName>>\r?$",
        options: RegexOptions.Multiline)
    ]
    private static partial Regex UnionDeclarationRegex { get; }

    public static void AssertGeneratedCode(string sourceCode, int sourceIndex, string expected)
    {
        var (_, generatedCodes, _, supportsOfficialUnion) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var expectedCode = ApplyOfficialUnionDeclaration(expected, supportsOfficialUnion);

        Assert.That(
            actual: generatedCodes[sourceIndex].ReplaceLineEndings(),
            expression: Is.EqualTo(expectedCode.ReplaceLineEndings())
        );

        #region Local Funcitons
        static string ApplyOfficialUnionDeclaration(string expectedCode, bool supportsOfficialUnion)
        {
            if (!supportsOfficialUnion)
            {
                return expectedCode;
            }

            return UnionDeclarationRegex.Replace(expectedCode, match =>
            {
                var indent = match.Groups["indent"].Value;
                var typeName = match.Groups["typeName"].Value;

                return $"{indent}[global::System.Runtime.CompilerServices.Union]\n{indent}partial struct {typeName} : global::System.IEquatable<{typeName}>, global::System.Runtime.CompilerServices.IUnion";
            });
        }
        #endregion
    }

    public static void AssertGeneratedCode(string sourceCode, string expected)
    {
        AssertGeneratedCode(sourceCode, sourceIndex: 0, expected);
    }

    public static void AssertGeneratedCodeContains(
        string sourceCode,
        int sourceIndex,
        params string[] expectedFragments
    )
    {
        var (_, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var generatedCode = generatedCodes[sourceIndex].ReplaceLineEndings();

        foreach (var expectedFragment in expectedFragments)
        {
            Assert.That(
                actual: generatedCode,
                expression: Does.Contain(expectedFragment.ReplaceLineEndings())
            );
        }
    }

    public static void AssertGeneratedCodeContains(string sourceCode, params string[] expectedFragments)
    {
        AssertGeneratedCodeContains(sourceCode, sourceIndex: 0, expectedFragments);
    }

    public static void AssertDiagnostic(string sourceCode, string expectedDiagnosticId)
    {
        AssertDiagnostics(sourceCode, expectedDiagnosticId);
    }

    public static void AssertDiagnostics(string sourceCode, params string[] expectedDiagnosticIds)
    {
        var (diagnostics, _, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var actualDiagnosticIds = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        Assert.That(actualDiagnosticIds, Is.EquivalentTo(expectedDiagnosticIds));
    }

    public static void AssertDiagnosticLocationText(
        string sourceCode,
        string expectedDiagnosticId,
        string expectedLocationText
    )
    {
        var (diagnostics, _, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );
        var errorDiagnostics = diagnostics
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        var actualDiagnosticIds = errorDiagnostics
            .Select(diagnostic => diagnostic.Id)
            .ToArray();

        Assert.That(actualDiagnosticIds, Is.EquivalentTo(new[] { expectedDiagnosticId }));

        var diagnostic = errorDiagnostics.Single(diagnostic => diagnostic.Id == expectedDiagnosticId);

        Assert.That(diagnostic.Location.SourceTree, Is.Not.Null);
        Assert.That(
            actual: diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan),
            expression: Is.EqualTo(expectedLocationText)
        );
    }

    public static (
        ImmutableArray<Diagnostic> diagnostics,
        string[] generatedCodes,
        string[] generatedHintNames,
        bool supportsOfficialUnion
    ) CompileAndGetResults<T>(
        string sourceCode,
        Assembly[]? additionalAssemblies = null
    ) where T : IIncrementalGenerator, new()
    {
        var generator = new T();
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode,
            additionalAssemblies,
            assemblyName: "Macaron.TaggedUnion.Tests",
            generators: [generator.AsSourceGenerator()]
        );

        var result = driver.RunGenerators(compilation).GetRunResult().Results.Single();
        var generatedSources = result.GeneratedSources;
        var generatedCodes = generatedSources.Select(source => source.SourceText.ToString()).ToArray();
        var generatedHintNames = generatedSources.Select(source => source.HintName).ToArray();

        var allDiagnostics = compilation.GetDiagnostics()
            .Concat(result.Diagnostics)
            .ToImmutableArray();

        return (allDiagnostics, generatedCodes, generatedHintNames, SupportsOfficialUnion(compilation));
    }

    public static (CSharpCompilation compilation, CSharpGeneratorDriver driver) CreateCompilationAndDriver(
        string sourceCode,
        Assembly[]? additionalAssemblies,
        string assemblyName,
        ISourceGenerator[] generators
    )
    {
        var references = AppDomain
            .CurrentDomain
            .GetAssemblies()
            .Concat(additionalAssemblies ?? [])
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => MetadataReference.CreateFromFile(assembly.Location))
            .Cast<MetadataReference>()
            .ToImmutableArray();

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, ParseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntaxTree],
            references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
        var driver = CSharpGeneratorDriver.Create(generators, parseOptions: ParseOptions);

        return (compilation, driver);
    }

    private static bool SupportsOfficialUnion(Compilation compilation)
    {
        var unionAttributeSymbol = compilation.GetTypeByMetadataName(UnionAttributeMetadataName);
        var unionInterfaceSymbol = compilation.GetTypeByMetadataName(UnionInterfaceMetadataName);

        return IsUnionAttribute(unionAttributeSymbol, compilation)
               && IsUnionInterface(unionInterfaceSymbol, compilation);

        #region Local Functions
        static bool IsUnionAttribute(INamedTypeSymbol? symbol, Compilation compilation)
        {
            if (symbol is not { TypeKind: TypeKind.Class, Arity: 0 })
            {
                return false;
            }

            var attributeSymbol = compilation.GetTypeByMetadataName(AttributeMetadataName);
            var currentSymbol = symbol;

            while (currentSymbol != null)
            {
                if (SymbolEqualityComparer.Default.Equals(currentSymbol, attributeSymbol))
                {
                    return true;
                }

                currentSymbol = currentSymbol.BaseType;
            }

            return false;
        }

        static bool IsUnionInterface(INamedTypeSymbol? symbol, Compilation compilation)
        {
            if (symbol is not { TypeKind: TypeKind.Interface, Arity: 0 })
            {
                return false;
            }

            var objectSymbol = compilation.GetSpecialType(SpecialType.System_Object);

            return symbol
                .GetMembers("Value")
                .OfType<IPropertySymbol>()
                .Any(propertySymbol =>
                    propertySymbol.Parameters.Length == 0
                    && propertySymbol.GetMethod is { DeclaredAccessibility: Accessibility.Public }
                    && SymbolEqualityComparer.Default.Equals(propertySymbol.Type, objectSymbol)
                );
        }
        #endregion
    }
}
