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

    [TestCase(typeof(ITaggedUnion<,>))]
    [TestCase(typeof(ITaggedUnion<,,>))]
    [TestCase(typeof(ITaggedUnion<,,,>))]
    [TestCase(typeof(ITaggedUnion<,,,,>))]
    [TestCase(typeof(ITaggedUnion<,,,,,>))]
    [TestCase(typeof(ITaggedUnion<,,,,,,>))]
    [TestCase(typeof(ITaggedUnion<,,,,,,,>))]
    public void GenericContractsContainCaseSpecificTryGetValueMembers(Type contractType)
    {
        var caseTypes = contractType.GetGenericArguments();
        var inheritedInterfaces = contractType.GetInterfaces();
        var tryGetValueMethods = contractType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.Name == nameof(ITaggedUnion<int, string>.TryGetValue))
            .ToArray();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inheritedInterfaces, Does.Contain(typeof(ITaggedUnion)));
            Assert.That(
                inheritedInterfaces,
                Has.None.Matches<Type>(
                    type => type.IsGenericType
                        && type.GetGenericTypeDefinition().Name.StartsWith("ITaggedUnionCases`", StringComparison.Ordinal)
                )
            );
            Assert.That(tryGetValueMethods, Has.Length.EqualTo(caseTypes.Length));

            foreach (var method in tryGetValueMethods)
            {
                var parameters = method.GetParameters();
                var parameter = parameters.Length == 1 ? parameters[0] : null;
                Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)), method.ToString());
                Assert.That(parameters, Has.Length.EqualTo(1), method.ToString());
                Assert.That(parameter?.IsOut, Is.True, method.ToString());
                Assert.That(
                    parameter?.GetCustomAttribute<NotNullWhenAttribute>()?.ReturnValue,
                    Is.True,
                    method.ToString()
                );
            }
        }
    }
}
