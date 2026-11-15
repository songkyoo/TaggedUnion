namespace Macaron.Union;

internal sealed record TaggedUnionGenerationModel(
    UnionGenerationModel Union,
    bool GenerateConversionOperators,
    string? GetTypeParameterName
);
