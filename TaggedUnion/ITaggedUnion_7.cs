using System.Diagnostics.CodeAnalysis;

namespace Macaron.Union;

public interface ITaggedUnion<T1, T2, T3, T4, T5, T6, T7> : ITaggedUnion
    where T1 : notnull
    where T2 : notnull
    where T3 : notnull
    where T4 : notnull
    where T5 : notnull
    where T6 : notnull
    where T7 : notnull
{
    bool TryGetValue([NotNullWhen(returnValue: true)] out T1? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T2? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T3? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T4? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T5? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T6? value);

    bool TryGetValue([NotNullWhen(returnValue: true)] out T7? value);
}
