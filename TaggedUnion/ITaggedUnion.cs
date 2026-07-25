namespace Macaron.Union;

public interface ITaggedUnion
{
    bool HasValue { get; }

    object? Value { get; }
}
