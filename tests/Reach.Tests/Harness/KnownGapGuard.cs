using System.Reflection;
using Reach.Tests.Acceptance;
using Xunit.v3;

namespace Reach.Tests.Harness;

/// <summary>
/// Whether known-gap tests are declared consistently (spec §4.5): the <c>Known_gap_</c> name and
/// the <c>Batch=known-gap</c> trait go together, or neither is there.
/// </summary>
/// <remarks>
/// The trait is what the release gate excludes and the name is what a reader sees, so a test with
/// one and not the other is either in the gate by mistake or wrongly labelled a gap.
/// </remarks>
internal static class KnownGapGuard
{
    internal const string Prefix = "Known_gap_";

    /// <summary>
    /// Every test method on a class xUnit runs in <paramref name="assembly"/>: a method with a
    /// fact attribute (<c>[Fact]</c>, <c>[Theory]</c>) on a concrete class, inherited ones included.
    /// </summary>
    internal static IReadOnlyList<MethodInfo> TestMethodsOf(Assembly assembly) =>
        [.. assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            .Where(method => method.GetCustomAttributes().OfType<IFactAttribute>().Any())];

    /// <summary>
    /// One line per method whose name and trait disagree, naming the method and what it lacks.
    /// </summary>
    internal static IReadOnlyList<string> Failures(IEnumerable<MethodInfo> methods) =>
        [.. methods
            .Select(method => (Method: method, Named: IsNamed(method), Traited: IsTraited(method)))
            .Where(test => test.Named != test.Traited)
            .Select(test => test.Named
                ? $"{NameOf(test.Method)} is named {Prefix} but lacks [KnownGap]."
                : $"{NameOf(test.Method)} has [KnownGap] but is not named {Prefix}.")
            .Order(StringComparer.Ordinal)];

    private static bool IsNamed(MethodInfo method) => method.Name.StartsWith(Prefix, StringComparison.Ordinal);

    private static bool IsTraited(MethodInfo method) =>
        method
            .GetCustomAttributes()
            .OfType<ITraitAttribute>()
            .SelectMany(attribute => attribute.GetTraits())
            .Contains(new(KnownGapAttribute.Name, KnownGapAttribute.Value));

    private static string NameOf(MethodInfo method) => $"{method.DeclaringType?.FullName}.{method.Name}";
}
