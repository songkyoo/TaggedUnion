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
        pattern: @"^(?<indent>[ \t]*)(?<declaration>readonly partial struct (?<typeName>@?[A-Za-z_][A-Za-z0-9_]*) : global::System\.IEquatable<\k<typeName>>, global::Macaron\.Union\.ITaggedUnion<.+>)\r?$",
        options: RegexOptions.Multiline)
    ]
    private static partial Regex UnionDeclarationRegex { get; }

    public static void AssertGeneratedCode(string sourceCode, string expected)
    {
        var (diagnostics, generatedCodes, _, supportsOfficialUnion) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );

        AssertNoErrors(diagnostics);

        var expectedCode = ApplyOfficialUnionDeclaration(expected, supportsOfficialUnion);

        Assert.That(
            actual: generatedCodes.Single().ReplaceLineEndings(),
            expression: Is.EqualTo(expectedCode.ReplaceLineEndings())
        );

        #region Local Functions
        static string ApplyOfficialUnionDeclaration(string expectedCode, bool supportsOfficialUnion)
        {
            if (!supportsOfficialUnion)
            {
                return expectedCode;
            }

            return UnionDeclarationRegex.Replace(expectedCode, match =>
            {
                var indent = match.Groups["indent"].Value;
                var declaration = match.Groups["declaration"].Value;

                return $"{indent}[global::System.Runtime.CompilerServices.Union]\n{indent}{declaration}, global::System.Runtime.CompilerServices.IUnion";
            });
        }
        #endregion
    }

    public static void AssertGeneratedCodeContains(string sourceCode, params string[] expectedFragments)
    {
        var (diagnostics, generatedCodes, _, _) = CompileAndGetResults<TaggedUnionGenerator>(
            sourceCode,
            additionalAssemblies: [typeof(TaggedUnionAttribute).Assembly]
        );

        AssertNoErrors(diagnostics);
        AssertContainsFragments(generatedCodes.Single(), expectedFragments);
    }

    public static void AssertContainsFragments(string generatedCode, params string[] expectedFragments)
    {
        var normalizedCode = generatedCode.ReplaceLineEndings("\n");

        using (Assert.EnterMultipleScope())
        {
            foreach (var fragment in expectedFragments)
            {
                Assert.That(normalizedCode, Does.Contain(fragment.ReplaceLineEndings("\n")));
            }
        }
    }

    public static void AssertNoErrors(IEnumerable<Diagnostic> diagnostics)
    {
        var errors = diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();

        Assert.That(errors, Is.Empty, string.Join<Diagnostic>(Environment.NewLine, errors));
    }

    public static Assembly EmitGeneratedAssembly(Compilation compilation, GeneratorDriver driver)
    {
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        AssertNoErrors(generatorDiagnostics);

        using var stream = new MemoryStream();
        var result = output.Emit(stream);

        Assert.That(result.Success, Is.True, string.Join(Environment.NewLine, result.Diagnostics));

        return Assembly.Load(stream.ToArray());
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

        AssertDiagnosticLocationText(
            diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            expectedDiagnosticId,
            expectedLocationText
        );
    }

    public static void AssertDiagnosticLocationText(
        IEnumerable<Diagnostic> diagnostics,
        string expectedDiagnosticId,
        string expectedLocationText
    )
    {
        var actual = diagnostics.ToArray();

        Assert.That(actual.Select(diagnostic => diagnostic.Id), Is.EqualTo(new[] { expectedDiagnosticId }));

        var location = actual.Single().Location;

        Assert.That(location.SourceTree?.GetText().ToString(location.SourceSpan), Is.EqualTo(expectedLocationText));
    }

    public static (
        ImmutableArray<Diagnostic> diagnostics,
        string[] generatedCodes,
        string[] generatedHintNames,
        bool supportsOfficialUnion
    ) CompileAndGetResults<T>(
        string sourceCode,
        Assembly[]? additionalAssemblies = null,
        IEnumerable<string>? preprocessorSymbols = null
    ) where T : IIncrementalGenerator, new()
    {
        var generator = new T();
        var (compilation, driver) = CreateCompilationAndDriver(
            sourceCode,
            additionalAssemblies,
            assemblyName: "Macaron.TaggedUnion.Tests",
            generators: [generator.AsSourceGenerator()],
            preprocessorSymbols: preprocessorSymbols
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
        ISourceGenerator[] generators,
        GeneratorDriverOptions? driverOptions = null,
        IEnumerable<string>? preprocessorSymbols = null
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

        var parseOptions = preprocessorSymbols != null
            ? ParseOptions.WithPreprocessorSymbols(preprocessorSymbols)
            : ParseOptions;
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, parseOptions);

        var compilation = CSharpCompilation.Create(
            assemblyName,
            syntaxTrees: [syntaxTree],
            references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );
        var driver = CSharpGeneratorDriver.Create(
            generators,
            additionalTexts: null,
            parseOptions,
            optionsProvider: null,
            driverOptions: driverOptions ?? default
        );

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
