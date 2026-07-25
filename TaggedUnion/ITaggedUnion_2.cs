using System.Diagnostics.CodeAnalysis;

namespace Macaron.Union;

public interface ITaggedUnion<T1, T2> : ITaggedUnion
    where T1 : notnull
    where T2 : notnull
{
    bool TryGetValue([NotNullWhen(returnValue: true)] out T1? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T2? value);
}
