using System.Diagnostics.CodeAnalysis;

namespace Macaron.Union;

public interface ITaggedUnion<T1, T2, T3> : ITaggedUnion
    where T1 : notnull
    where T2 : notnull
    where T3 : notnull
{
    bool TryGetValue([NotNullWhen(returnValue: true)] out T1? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T2? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T3? value);
}
