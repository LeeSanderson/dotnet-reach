using System.Reflection.Metadata;

namespace Reach.Selection;

/// <summary>
/// One row of the recognition table: which assembly a test project references, which versions
/// of it this row covers, and which attributes mark a test method.
/// </summary>
/// <param name="Dialect">
/// The filter expression grammar this combination renders into. Gated on package version as
/// well as framework, because <c>xunit.v3</c> 4.0.0 changed the filter surface.
/// </param>
internal sealed record TestFramework(
    string Name,
    string Assembly,
    Version Minimum,
    Version Exclusive,
    IReadOnlyList<string> TestAttributes,
    string Dialect)
{
    internal bool Covers(string assembly, Version version) =>
        string.Equals(Assembly, assembly, StringComparison.OrdinalIgnoreCase)
        && version >= Minimum
        && version < Exclusive;

    public override string ToString() => $"{Name} ({Dialect})";
}

/// <summary>
/// <strong>Test recognition is data, not code.</strong> Adding a framework touches no graph
/// code and no walk code — it is a row here. This is the one place the seam genuinely varies,
/// and a table is cheaper than four adapters.
/// </summary>
internal static class TestFrameworks
{
    private const string Xunit = "Xunit";
    private const string NUnit = "NUnit.Framework";
    private const string MsTest = "Microsoft.VisualStudio.TestTools.UnitTesting";

    /// <summary>
    /// <strong>There is no xUnit v4.</strong> The NuGet package <c>xunit.v3</c> is at
    /// <em>version</em> 4.0.0, where "v3" is the generation — and that release changed the
    /// filter surface, so the dialect is gated on the version as well as the framework.
    /// </summary>
    internal static IReadOnlyList<TestFramework> Table { get; } =
    [
        new(
            "xUnit v2",
            "xunit.core",
            new Version(2, 0),
            new Version(3, 0),
            [$"{Xunit}.FactAttribute", $"{Xunit}.TheoryAttribute"],
            "xunit-v2"),

        new(
            "xUnit v3",
            "xunit.v3.core",
            new Version(1, 0),
            new Version(4, 0),
            [$"{Xunit}.FactAttribute", $"{Xunit}.TheoryAttribute"],
            "xunit-v3"),

        new(
            "xUnit v3",
            "xunit.v3.core",
            new Version(4, 0),
            new Version(99, 0),
            [$"{Xunit}.FactAttribute", $"{Xunit}.TheoryAttribute"],
            "xunit-v3-4"),

        new(
            "NUnit",
            "nunit.framework",
            new Version(3, 0),
            new Version(99, 0),
            [
                $"{NUnit}.TestAttribute",
                $"{NUnit}.TestCaseAttribute",
                $"{NUnit}.TestCaseSourceAttribute",
                $"{NUnit}.TheoryAttribute",
            ],
            "nunit"),

        new(
            "MSTest",
            "Microsoft.VisualStudio.TestPlatform.TestFramework",
            new Version(2, 0),
            new Version(99, 0),
            [$"{MsTest}.TestMethodAttribute", $"{MsTest}.DataTestMethodAttribute"],
            "mstest"),
    ];

    /// <summary>
    /// Which framework an assembly is written against, read from the identities it references.
    /// Null when none of them is recognised, which is whole-project selection.
    /// </summary>
    internal static TestFramework? DetectedIn(MetadataReader reader)
    {
        foreach (var handle in reader.AssemblyReferences)
        {
            var reference = reader.GetAssemblyReference(handle);
            var name = reader.GetString(reference.Name);

            // Later rows win on a tie, so a version-specific row placed after a general one
            // takes precedence.
            var match = Table.LastOrDefault(framework => framework.Covers(name, reference.Version));

            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
