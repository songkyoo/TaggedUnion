using System.ComponentModel;
using System.Reflection;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class RuntimeTaggedUnionTests
{
    [Test]
    public void TaggedUnionAttributeUsesWritableStateParameterProperty()
    {
        var constructors = typeof(TaggedUnionAttribute).GetConstructors();
        var attribute = new TaggedUnionAttribute();

        Assert.Multiple(() =>
        {
            Assert.That(constructors, Has.Length.EqualTo(1));
            Assert.That(constructors[0].GetParameters(), Is.Empty);
            Assert.That(attribute.StateParamName, Is.EqualTo("state"));
        });

        attribute.StateParamName = "context";

        Assert.That(attribute.StateParamName, Is.EqualTo("context"));
    }

    [TestCase(2, typeof(TaggedUnion<,>))]
    [TestCase(3, typeof(TaggedUnion<,,>))]
    [TestCase(4, typeof(TaggedUnion<,,,>))]
    [TestCase(5, typeof(TaggedUnion<,,,,>))]
    [TestCase(6, typeof(TaggedUnion<,,,,,>))]
    [TestCase(7, typeof(TaggedUnion<,,,,,,>))]
    [TestCase(8, typeof(TaggedUnion<,,,,,,,>))]
    public void GenericTypesCoverAritiesFromTwoThroughEight(int arity, Type unionType)
    {
        var staticMethods = unionType.GetMethods(BindingFlags.Public | BindingFlags.Static);
        var instanceMethods = unionType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(unionType.GetGenericArguments(), Has.Length.EqualTo(arity));
            Assert.That(
                unionType.GetInterfaces(),
                Has.Some.Matches<Type>(
                    type => type.IsGenericType && type.GetGenericTypeDefinition().Name == $"ITaggedUnion`{arity}"
                )
            );
            Assert.That(unionType.GetConstructors(), Has.Length.EqualTo(arity));
            Assert.That(staticMethods.Count(method => method.Name == "op_Implicit"), Is.EqualTo(arity));
            Assert.That(staticMethods.Count(method => method.Name == nameof(TaggedUnion<int, string>.GetTag)), Is.EqualTo(1));
            Assert.That(
                instanceMethods.Count(method => method.Name == nameof(TaggedUnion<int, string>.Get)
                    && method.IsGenericMethodDefinition
                    && method.GetGenericArguments().Length == 1
                    && method.GetParameters().Length == 0),
                Is.EqualTo(1)
            );
            Assert.That(instanceMethods.Count(method => method.Name == nameof(TaggedUnion<int, string>.TryGetValue)), Is.EqualTo(arity));
            Assert.That(instanceMethods.Count(method => method.Name == nameof(TaggedUnion<int, string>.Switch)), Is.EqualTo(2));
            Assert.That(instanceMethods.Count(method => method.Name == nameof(TaggedUnion<int, string>.Match)), Is.EqualTo(2));
        }
    }

    [Test]
    public void DefaultValueIsUninitialized()
    {
        var value = default(TaggedUnion<int, string>);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(value.HasValue, Is.False);
            Assert.That(value.Value, Is.Null);
            Assert.That(value.ToString(), Is.EqualTo("<Uninitialized>"));
            Assert.That(value.GetHashCode(), Is.Zero);
            Assert.That(value, Is.EqualTo(default(TaggedUnion<int, string>)));
        }
    }

    [Test]
    public void TryGetValueReturnsFalseAndResetsTheOutputForAnUninitializedUnion()
    {
        var value = default(TaggedUnion<int, string>);
        int number = 42;
        string? text = "value";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(value.TryGetValue(out number), Is.False);
            Assert.That(number, Is.Zero);
            Assert.That(value.TryGetValue(out text), Is.False);
            Assert.That(text, Is.Null);
        }
    }

    [Test]
    public void SwitchRejectsAnUninitializedUnion()
    {
        Assert.Throws<InvalidOperationException>(() => default(TaggedUnion<int, string>).Switch(_ => { }, _ => { }));
    }

    [Test]
    public void MatchRejectsAnUninitializedUnion()
    {
        Assert.Throws<InvalidOperationException>(() => default(TaggedUnion<int, string>).Match(_ => 1, _ => 2));
    }

    [Test]
    public void GetRequiresTheExactTypeOfTheActiveCase()
    {
        var number = new TaggedUnion<int, string>(42);
        IComparable text = "value";
        var interfaceCase = new TaggedUnion<string, IComparable>(text);

        Assert.Multiple(() =>
        {
            Assert.That(number.Get<int>(), Is.EqualTo(42));
            Assert.That(interfaceCase.Get<IComparable>(), Is.SameAs(text));
            Assert.Throws<InvalidCastException>(() => number.Get<string>());
            Assert.Throws<InvalidCastException>(() => number.Get<object>());
            Assert.Throws<InvalidCastException>(() => interfaceCase.Get<string>());
            Assert.Throws<InvalidCastException>(() => default(TaggedUnion<int, string>).Get<int>());
        });
    }

    [Test]
    public void GetReadsTheActivePositionWhenCaseTypesRepeat()
    {
        var first = CreateFirst<int, int>(41);
        var second = CreateSecond<int, int>(42);

        Assert.Multiple(() =>
        {
            Assert.That(TaggedUnion<int, int>.GetTag(in first), Is.EqualTo(1));
            Assert.That(TaggedUnion<int, int>.GetTag(in second), Is.EqualTo(2));
            Assert.That(first.Get<int>(), Is.EqualTo(41));
            Assert.That(second.Get<int>(), Is.EqualTo(42));
        });

        #region Local Functions
        static TaggedUnion<T1, T2> CreateFirst<T1, T2>(T1 value)
            where T1 : notnull
            where T2 : notnull
        {
            return new TaggedUnion<T1, T2>(value);
        }

        static TaggedUnion<T1, T2> CreateSecond<T1, T2>(T2 value)
            where T1 : notnull
            where T2 : notnull
        {
            return new TaggedUnion<T1, T2>(value);
        }
        #endregion
    }

    [Test]
    public void GetTagReturnsThePositionalTag()
    {
        var defaultValue = default(TaggedUnion<int, string>);
        var intValue = new TaggedUnion<int, string>(42);
        var stringValue = new TaggedUnion<int, string>("value");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(TaggedUnion<int, string>.GetTag(in defaultValue), Is.Zero);
            Assert.That(TaggedUnion<int, string>.GetTag(in intValue), Is.EqualTo(1));
            Assert.That(TaggedUnion<int, string>.GetTag(in stringValue), Is.EqualTo(2));
        }
    }

    [Test]
    public void GetTagIsHiddenFromEditorCompletion()
    {
        var getTagMethod = typeof(TaggedUnion<int, string>).GetMethod(
            nameof(TaggedUnion<int, string>.GetTag),
            BindingFlags.Public | BindingFlags.Static
        )!;
        var editorBrowsable = getTagMethod.GetCustomAttribute<EditorBrowsableAttribute>();

        Assert.That(editorBrowsable?.State, Is.EqualTo(EditorBrowsableState.Never));
    }

    [Test]
    public void ImplicitConversionsStoreTheCaseValue()
    {
        TaggedUnion<int, string> number = 42;
        TaggedUnion<int, string> text = "value";

        using (Assert.EnterMultipleScope())
        {
            Assert.That(number.HasValue, Is.True);
            Assert.That(number.Value, Is.EqualTo(42));
            Assert.That(text.HasValue, Is.True);
            Assert.That(text.Value, Is.EqualTo("value"));
        }
    }

    [Test]
    public void TryGetValueReturnsOnlyTheActiveCase()
    {
        var number = new TaggedUnion<int, string>(42);
        var text = new TaggedUnion<int, string>("value");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(number.TryGetValue(out int actualNumber), Is.True);
            Assert.That(actualNumber, Is.EqualTo(42));
            Assert.That(number.TryGetValue(out string? missingText), Is.False);
            Assert.That(missingText, Is.Null);
            Assert.That(text.TryGetValue(out string? actualText), Is.True);
            Assert.That(actualText, Is.EqualTo("value"));
        }
    }

    [Test]
    public void ExplicitConversionsRequireTheActiveCase()
    {
        var number = new TaggedUnion<int, string>(42);
        var text = new TaggedUnion<int, string>("value");

        using (Assert.EnterMultipleScope())
        {
            Assert.That((int)number, Is.EqualTo(42));
            Assert.That((string)text, Is.EqualTo("value"));
            Assert.Throws<InvalidCastException>(() => _ = (string)number);
        }
    }

    [Test]
    public void EqualityComparesTheCaseAndItsValue()
    {
        var value = new TaggedUnion<int, string>(42);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(value, Is.EqualTo(new TaggedUnion<int, string>(42)));
            Assert.That(value, Is.Not.EqualTo(new TaggedUnion<int, string>(43)));
            Assert.That(value, Is.Not.EqualTo(new TaggedUnion<int, string>("42")));
        }
    }

    [Test]
    public void ConstructorRejectsNullForAReferenceCase()
    {
        Assert.Throws<ArgumentNullException>(() => _ = new TaggedUnion<int, string>(null!));
    }

    [Test]
    public void SwitchInvokesOnlyTheActiveCase()
    {
        var value = new TaggedUnion<int, string>("value");
        var calls = new List<string>();

        value.Switch(number => calls.Add(number.ToString()), calls.Add);

        Assert.That(calls, Is.EqualTo(new[] { "value" }));
    }

    [Test]
    public void MatchReturnsTheActiveCaseResult()
    {
        var value = new TaggedUnion<int, string>("value");

        Assert.That(value.Match(number => number.ToString(), text => text), Is.EqualTo("value"));
    }

    [Test]
    public void SwitchPassesStateToTheActiveCase()
    {
        var value = new TaggedUnion<int, string>("value");
        var calls = new List<string>();

        value.Switch(
            calls,
            static (number, state) => state.Add(number.ToString()),
            static (text, state) => state.Add(text)
        );

        Assert.That(calls, Is.EqualTo(new[] { "value" }));
    }

    [Test]
    public void MatchPassesStateToTheActiveCase()
    {
        var value = new TaggedUnion<int, string>("value");
        var result = value.Match(
            "matched:",
            static (number, prefix) => prefix + number,
            static (text, prefix) => prefix + text
        );

        Assert.That(result, Is.EqualTo("matched:value"));
    }

    [Test]
    public void EightCaseUnionSupportsTheLastCase()
    {
        var value = new TaggedUnion<int, long, short, byte, float, double, decimal, string>("value");
        ITaggedUnion<int, long, short, byte, float, double, decimal, string> contract = value;
        var matchedCase = value.Match(_ => 1, _ => 2, _ => 3, _ => 4, _ => 5, _ => 6, _ => 7, _ => 8);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(contract.TryGetValue(out string? result), Is.True);
            Assert.That(result, Is.EqualTo("value"));
            Assert.That(matchedCase, Is.EqualTo(8));
            Assert.That((string)value, Is.EqualTo("value"));
            Assert.That(TaggedUnion<int, long, short, byte, float, double, decimal, string>.GetTag(in value), Is.EqualTo(8));
        }
    }

    [Test]
    public void OfficialUnionMembersProviderIsTargetFrameworkSpecific()
    {
        var taggedUnionType = typeof(TaggedUnion<,>);
        var unionAttributeName = "System.Runtime.CompilerServices.UnionAttribute";
        var hasUnionAttribute = taggedUnionType
            .CustomAttributes
            .Any(attribute => attribute.AttributeType.FullName == unionAttributeName);
        var providerType = taggedUnionType.GetNestedType(
            "IUnionMembers",
            BindingFlags.Public
        );

#if NET11_0_OR_GREATER
        Assert.Multiple(() =>
        {
            Assert.That(hasUnionAttribute, Is.True);
            Assert.That(providerType, Is.Not.Null);
            Assert.That(
                providerType!.GetMethods(BindingFlags.Public | BindingFlags.Static),
                Has.Exactly(2).Matches<MethodInfo>(method => method.Name == "Create")
            );
            Assert.That(
                taggedUnionType.GetInterfaces(),
                Has.Some.Matches<Type>(
                    type => type.IsNested
                        && type.Name == providerType.Name
                        && type.DeclaringType == taggedUnionType
                )
            );
        });
#else
        Assert.Multiple(() =>
        {
            Assert.That(hasUnionAttribute, Is.False);
            Assert.That(providerType, Is.Null);
        });
#endif
    }
}
