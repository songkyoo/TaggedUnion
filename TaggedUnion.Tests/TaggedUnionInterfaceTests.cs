using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Macaron.Union.Tests;

[TestFixture]
public sealed class TaggedUnionInterfaceTests
{
    [Test]
    public void NonGenericContractContainsCommonValueMembers()
    {
        var contractType = typeof(ITaggedUnion);

        Assert.Multiple(() =>
        {
            Assert.That(
                contractType.GetProperty(nameof(ITaggedUnion.HasValue))?.PropertyType,
                Is.EqualTo(typeof(bool))
            );
            Assert.That(
                contractType.GetProperty(nameof(ITaggedUnion.Value))?.PropertyType,
                Is.EqualTo(typeof(object))
            );
        });
    }

    [Test]
    public void GenericContractsContainCaseSpecificTryGetValueMembers()
    {
        Type[] contractTypes =
        [
            typeof(ITaggedUnion<,>),
            typeof(ITaggedUnion<,,>),
            typeof(ITaggedUnion<,,,>),
            typeof(ITaggedUnion<,,,,>),
            typeof(ITaggedUnion<,,,,,>),
            typeof(ITaggedUnion<,,,,,,>),
            typeof(ITaggedUnion<,,,,,,,>),
        ];

        Assert.Multiple(() =>
        {
            foreach (var contractType in contractTypes)
            {
                var arity = contractType.GetGenericArguments().Length;
                var inheritedInterfaces = contractType.GetInterfaces();
                var tryGetValueMethods = contractType
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                    .Where(method => method.Name == nameof(ITaggedUnion<int, string>.TryGetValue))
                    .ToArray();

                Assert.That(
                    inheritedInterfaces,
                    Has.Some.EqualTo(typeof(ITaggedUnion)),
                    contractType.FullName
                );
                Assert.That(
                    inheritedInterfaces,
                    Has.None.Matches<Type>(
                        type => type.IsGenericType
                            && type.GetGenericTypeDefinition().Name.StartsWith("ITaggedUnionCases`")
                    ),
                    contractType.FullName
                );
                Assert.That(tryGetValueMethods, Has.Length.EqualTo(arity), contractType.FullName);

                foreach (var method in tryGetValueMethods)
                {
                    var parameters = method.GetParameters();

                    Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)), method.ToString());
                    Assert.That(parameters, Has.Length.EqualTo(1), method.ToString());
                    Assert.That(parameters[0].IsOut, Is.True, method.ToString());
                    Assert.That(
                        parameters[0].GetCustomAttribute<NotNullWhenAttribute>()?.ReturnValue,
                        Is.True,
                        method.ToString()
                    );
                }
            }
        });
    }
}
