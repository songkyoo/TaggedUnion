using System.ComponentModel;
using System.Reflection;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class RuntimeTaggedUnionTests
{
    [Test]
    public void GenericTypesCoverAritiesFromTwoThroughEight()
    {
        Type[] taggedUnionTypes =
        [
            typeof(TaggedUnion<,>),
            typeof(TaggedUnion<,,>),
            typeof(TaggedUnion<,,,>),
            typeof(TaggedUnion<,,,,>),
            typeof(TaggedUnion<,,,,,>),
            typeof(TaggedUnion<,,,,,,>),
            typeof(TaggedUnion<,,,,,,,>),
        ];

        Assert.Multiple(() =>
        {
            for (var index = 0; index < taggedUnionTypes.Length; index++)
            {
                var taggedUnionType = taggedUnionTypes[index];
                var arity = index + 2;

                Assert.That(
                    taggedUnionType.GetGenericArguments(),
                    Has.Length.EqualTo(arity),
                    taggedUnionType.FullName
                );
                Assert.That(
                    taggedUnionType.GetInterfaces(),
                    Has.Some.Matches<Type>(
                        type => type.IsGenericType
                            && type.GetGenericTypeDefinition().Name == $"ITaggedUnion`{arity}"
                    ),
                    taggedUnionType.FullName
                );
                Assert.That(
                    taggedUnionType.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
                    Has.Length.EqualTo(arity),
                    taggedUnionType.FullName
                );
                Assert.That(
                    taggedUnionType
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Count(method => method.Name == "op_Implicit"),
                    Is.EqualTo(arity),
                    taggedUnionType.FullName
                );
                Assert.That(
                    taggedUnionType
                        .GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .Count(method => method.Name == nameof(TaggedUnion<int, string>.GetTag)),
                    Is.EqualTo(1),
                    taggedUnionType.FullName
                );
                Assert.That(
                    taggedUnionType
                        .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                        .Count(method => method.Name == nameof(TaggedUnion<int, string>.TryGetValue)),
                    Is.EqualTo(arity),
                    taggedUnionType.FullName
                );
            }
        });
    }

    [Test]
    public void DefaultValueIsUninitialized()
    {
        var value = default(TaggedUnion<int, string>);

        var hasInt = value.TryGetValue(out int intValue);
        var hasString = value.TryGetValue(out string? stringValue);

        Assert.Multiple(() =>
        {
            Assert.That(value.HasValue, Is.False);
            Assert.That(value.Value, Is.Null);
            Assert.That(value.ToString(), Is.EqualTo("<Uninitialized>"));
            Assert.That(value.GetHashCode(), Is.Zero);
            Assert.That(value, Is.EqualTo(default(TaggedUnion<int, string>)));
            Assert.That(hasInt, Is.False);
            Assert.That(intValue, Is.Zero);
            Assert.That(hasString, Is.False);
            Assert.That(stringValue, Is.Null);
            Assert.That(
                () => value.Switch(_ => { }, _ => { }),
                Throws.TypeOf<InvalidOperationException>()
            );
            Assert.That(
                () => value.Match(_ => 1, _ => 2),
                Throws.TypeOf<InvalidOperationException>()
            );
        });
    }

    [Test]
    public void GetTagReturnsThePositionalTagAndIsHiddenFromEditorCompletion()
    {
        var defaultValue = default(TaggedUnion<int, string>);
        var intValue = new TaggedUnion<int, string>(42);
        var stringValue = new TaggedUnion<int, string>("value");
        var getTagMethod = typeof(TaggedUnion<int, string>).GetMethod(
            nameof(TaggedUnion<int, string>.GetTag),
            BindingFlags.Public | BindingFlags.Static
        )!;
        var editorBrowsable = getTagMethod.GetCustomAttribute<EditorBrowsableAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(TaggedUnion<int, string>.GetTag(in defaultValue), Is.Zero);
            Assert.That(TaggedUnion<int, string>.GetTag(in intValue), Is.EqualTo(1));
            Assert.That(TaggedUnion<int, string>.GetTag(in stringValue), Is.EqualTo(2));
            Assert.That(editorBrowsable?.State, Is.EqualTo(EditorBrowsableState.Never));
        });
    }

    [Test]
    public void ValueCanBeCreatedAndConvertedForEachCase()
    {
        TaggedUnion<int, string> intUnion = 42;
        TaggedUnion<int, string> stringUnion = "value";

        var intUnionHasInt = intUnion.TryGetValue(out int intValue);
        var intUnionHasString = intUnion.TryGetValue(out string? missingString);
        var stringUnionHasString = stringUnion.TryGetValue(out string? stringValue);

        Assert.Multiple(() =>
        {
            Assert.That(intUnion.HasValue, Is.True);
            Assert.That(intUnion.Value, Is.EqualTo(42));
            Assert.That(intUnionHasInt, Is.True);
            Assert.That(intValue, Is.EqualTo(42));
            Assert.That(intUnionHasString, Is.False);
            Assert.That(missingString, Is.Null);
            Assert.That((int)intUnion, Is.EqualTo(42));
            Assert.That(
                () => _ = (string)intUnion,
                Throws.TypeOf<InvalidCastException>()
            );

            Assert.That(stringUnion.Value, Is.EqualTo("value"));
            Assert.That(stringUnionHasString, Is.True);
            Assert.That(stringValue, Is.EqualTo("value"));
            Assert.That((string)stringUnion, Is.EqualTo("value"));

            Assert.That(intUnion, Is.EqualTo(new TaggedUnion<int, string>(42)));
            Assert.That(intUnion, Is.Not.EqualTo(new TaggedUnion<int, string>(43)));
            Assert.That(intUnion, Is.Not.EqualTo(stringUnion));
            Assert.That(
                () => _ = new TaggedUnion<int, string>(null!),
                Throws.TypeOf<ArgumentNullException>()
            );
        });
    }

    [Test]
    public void SwitchAndMatchUseTheActiveCase()
    {
        var value = new TaggedUnion<int, string>("value");
        string? switchedValue = null;

        value.Switch(
            intValue => switchedValue = intValue.ToString(),
            stringValue => switchedValue = stringValue
        );
        var matchedValue = value.Match(
            intValue => intValue.ToString(),
            stringValue => stringValue
        );

        Assert.Multiple(() =>
        {
            Assert.That(switchedValue, Is.EqualTo("value"));
            Assert.That(matchedValue, Is.EqualTo("value"));
        });
    }

    [Test]
    public void EightCaseTypeImplementsTheOperationalContract()
    {
        var value = new TaggedUnion<
            Case1,
            Case2,
            Case3,
            Case4,
            Case5,
            Case6,
            Case7,
            Case8
        >(new Case8("value"));
        ITaggedUnion<
            Case1,
            Case2,
            Case3,
            Case4,
            Case5,
            Case6,
            Case7,
            Case8
        > contract = value;

        var hasValue = contract.TryGetValue(out Case8? result);
        var matchedCase = value.Match(
            _ => 1,
            _ => 2,
            _ => 3,
            _ => 4,
            _ => 5,
            _ => 6,
            _ => 7,
            _ => 8
        );

        Assert.Multiple(() =>
        {
            Assert.That(hasValue, Is.True);
            Assert.That(result?.Value, Is.EqualTo("value"));
            Assert.That(matchedCase, Is.EqualTo(8));
            Assert.That(((Case8)value).Value, Is.EqualTo("value"));
            Assert.That(
                TaggedUnion<
                    Case1,
                    Case2,
                    Case3,
                    Case4,
                    Case5,
                    Case6,
                    Case7,
                    Case8
                >.GetTag(in value),
                Is.EqualTo(8)
            );
        });
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

    private sealed record Case1(string Value);

    private sealed record Case2(string Value);

    private sealed record Case3(string Value);

    private sealed record Case4(string Value);

    private sealed record Case5(string Value);

    private sealed record Case6(string Value);

    private sealed record Case7(string Value);

    private sealed record Case8(string Value);
}
