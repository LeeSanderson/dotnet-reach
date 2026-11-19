using System.Reflection.Metadata;

namespace Reach.Selection;

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
    /// <c>IAsyncLifetime</c>, and fixtures: a class fixture is the test class's own, while a
    /// collection fixture is taken as the whole assembly's — matching collection names would be
    /// exact, and over-selecting a few classes is the safe direction.
    /// </summary>
    private static readonly TestLifecycleHooks XunitLifecycle = new(
        MethodNames: ["InitializeAsync"],
        MethodAttributes: [],
        AssemblyMethodAttributes: [],
        AssemblyTypeAttributes: [],
        ClassFixtureInterfaces: [$"{Xunit}.IClassFixture`1"],
        AssemblyFixtureInterfaces: [$"{Xunit}.ICollectionFixture`1"],
        AssemblyFixtureAttributes: [$"{Xunit}.AssemblyFixtureAttribute"]);

    /// <summary>
    /// A <c>[SetUpFixture]</c> runs around every fixture in its namespace and below; it is taken
    /// as the whole assembly's, which is the safe direction.
    /// </summary>
    private static readonly TestLifecycleHooks NUnitLifecycle = new(
        MethodNames: [],
        MethodAttributes:
        [
            $"{NUnit}.SetUpAttribute",
            $"{NUnit}.TearDownAttribute",
            $"{NUnit}.OneTimeSetUpAttribute",
            $"{NUnit}.OneTimeTearDownAttribute",
        ],
        AssemblyMethodAttributes: [],
        AssemblyTypeAttributes: [$"{NUnit}.SetUpFixtureAttribute"],
        ClassFixtureInterfaces: [],
        AssemblyFixtureInterfaces: [],
        AssemblyFixtureAttributes: []);

    private static readonly TestLifecycleHooks MsTestLifecycle = new(
        MethodNames: [],
        MethodAttributes:
        [
            $"{MsTest}.TestInitializeAttribute",
            $"{MsTest}.TestCleanupAttribute",
            $"{MsTest}.ClassInitializeAttribute",
            $"{MsTest}.ClassCleanupAttribute",
        ],
        AssemblyMethodAttributes:
        [
            $"{MsTest}.AssemblyInitializeAttribute",
            $"{MsTest}.AssemblyCleanupAttribute",
            $"{MsTest}.GlobalTestInitializeAttribute",
            $"{MsTest}.GlobalTestCleanupAttribute",
        ],
        AssemblyTypeAttributes: [],
        ClassFixtureInterfaces: [],
        AssemblyFixtureInterfaces: [],
        AssemblyFixtureAttributes: []);

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
            "xunit-v2",
            XunitLifecycle),

        new(
            "xUnit v3",
            "xunit.v3.core",
            new Version(1, 0),
            new Version(4, 0),
            [$"{Xunit}.FactAttribute", $"{Xunit}.TheoryAttribute"],
            "xunit-v3",
            XunitLifecycle),

        new(
            "xUnit v3",
            "xunit.v3.core",
            new Version(4, 0),
            new Version(99, 0),
            [$"{Xunit}.FactAttribute", $"{Xunit}.TheoryAttribute"],
            "xunit-v3-4",
            XunitLifecycle),

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
            "nunit",
            NUnitLifecycle),

        new(
            "MSTest",
            "Microsoft.VisualStudio.TestPlatform.TestFramework",
            new Version(2, 0),
            new Version(99, 0),
            [$"{MsTest}.TestMethodAttribute", $"{MsTest}.DataTestMethodAttribute"],
            "mstest",
            MsTestLifecycle),
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

/// <summary>
/// One row of the recognition table: which assembly a test project references, which versions
/// of it this row covers, which attributes mark a test method, and what else runs around one.
/// </summary>
/// <param name="Name">The framework's name, for the report.</param>
/// <param name="Assembly">The simple name of the assembly a test project references.</param>
/// <param name="Minimum">The lowest version this row covers.</param>
/// <param name="Exclusive">The first version this row no longer covers.</param>
/// <param name="TestAttributes">The full names of the attributes that mark a test method.</param>
/// <param name="Dialect">
/// The filter expression grammar this combination renders into. Gated on package version as
/// well as framework, because <c>xunit.v3</c> 4.0.0 changed the filter surface.
/// </param>
/// <param name="Lifecycle">What the framework runs around a test besides the test method.</param>
internal sealed record TestFramework(
    string Name,
    string Assembly,
    Version Minimum,
    Version Exclusive,
    IReadOnlyList<string> TestAttributes,
    string Dialect,
    TestLifecycleHooks Lifecycle)
{
    internal bool Covers(string assembly, Version version) =>
        string.Equals(Assembly, assembly, StringComparison.OrdinalIgnoreCase)
        && version >= Minimum
        && version < Exclusive;

    public override string ToString() => $"{Name} ({Dialect})";
}

/// <summary>
/// What a framework runs around a test besides the test method, beyond the constructors,
/// static constructor and disposal that every framework runs on an instance it creates.
/// </summary>
/// <param name="MethodNames">
/// Methods the framework calls by contract on a test class, matched by name, explicit
/// implementations included.
/// </param>
/// <param name="MethodAttributes">Attributes marking a method run around each test in its class.</param>
/// <param name="AssemblyMethodAttributes">
/// Attributes marking a method run around every test in the assembly.
/// </param>
/// <param name="AssemblyTypeAttributes">
/// Attributes marking a class whose lifecycle runs around every test in the assembly.
/// </param>
/// <param name="ClassFixtureInterfaces">
/// Generic interfaces on a test class whose type argument the framework creates for it.
/// </param>
/// <param name="AssemblyFixtureInterfaces">
/// Generic interfaces on any class whose type argument the framework creates for the assembly.
/// </param>
/// <param name="AssemblyFixtureAttributes">
/// Assembly attributes whose <c>typeof</c> argument the framework creates for the assembly.
/// </param>
internal sealed record TestLifecycleHooks(
    IReadOnlyList<string> MethodNames,
    IReadOnlyList<string> MethodAttributes,
    IReadOnlyList<string> AssemblyMethodAttributes,
    IReadOnlyList<string> AssemblyTypeAttributes,
    IReadOnlyList<string> ClassFixtureInterfaces,
    IReadOnlyList<string> AssemblyFixtureInterfaces,
    IReadOnlyList<string> AssemblyFixtureAttributes);
