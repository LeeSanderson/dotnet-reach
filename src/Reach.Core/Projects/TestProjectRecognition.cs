namespace Reach.Projects;

/// <summary>
/// Whether a project declares tests, from the test-framework packages it references.
/// </summary>
/// <remarks>
/// A minimal stand-in. The recognition table proper — which framework, which version, which
/// attributes name a test method, which filter dialect it renders into — is data rather than
/// code and arrives with test recognition. Nothing outside this file should grow a second
/// opinion about what a test project is.
/// </remarks>
internal static class TestProjectRecognition
{
    /// <summary>
    /// Prefixes, not exact ids: <c>xunit.v3</c> arrives as <c>xunit.v3.mtp-v2</c> through its
    /// own metapackage, and matching the family is the widening answer.
    /// </summary>
    private static readonly string[] TestFrameworkPackages =
    [
        "xunit",
        "NUnit",
        "MSTest",
        "TUnit",
        "Microsoft.NET.Test.Sdk",
        "Microsoft.Testing.Platform",
    ];

    internal static bool DeclaresTests(ProjectFile project) =>
        project.PackageReferences.Any(IsTestFramework);

    private static bool IsTestFramework(string packageId) =>
        TestFrameworkPackages.Any(known =>
            packageId.Equals(known, StringComparison.OrdinalIgnoreCase)
            || packageId.StartsWith(known + ".", StringComparison.OrdinalIgnoreCase));
}
