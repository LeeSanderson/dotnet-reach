using Reach.Assemblies;
using Reach.Projects;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Assemblies;

/// <summary>
/// Discovery against real assemblies in real directories. Relocating already-built output is
/// the cheap way to test this: no project per layout, and every layout the SDK can produce is
/// just a different place to put the same file.
/// </summary>
public class AssemblyDiscoveryTests
{
    private const string TestPackage = "xunit.v3";

    private const string CoreSource = "namespace N; public class Widget { public int Spin() => 1; }";
    private const string TestsSource = "namespace N.Tests; public class WidgetTests { public void Spins() { } }";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A solution with Core and Tests, with no build output anywhere yet.</summary>
    private static async Task<(ProjectTree Tree, AnalysisScope Scope)> SolutionAsync(
        string coreFrameworks = "net10.0")
    {
        var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: coreFrameworks);
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");
        var solution = tree.AddSlnx("Solution", core, tests);

        var discovery = TargetDiscovery.Discover(solution);
        Assert.True(discovery.Discovered, discovery.Message);

        var scope = await AnalysisScopeResolver.ResolveAsync(discovery.Target!, Token);
        Assert.True(scope.Resolved, scope.Message);

        return (tree, scope.Scope!);
    }

    private static string Emit(
        ProjectTree tree,
        string project,
        string source,
        string outputDirectory,
        string targetFramework = ".NETCoreApp,Version=v10.0",
        string? targetPlatform = null,
        string? assemblyName = null) =>
        Compiled
            .Assembly(
                assemblyName ?? project,
                source,
                // Inside the working tree, which is what makes it first-party.
                tree.Combine("src", project, project + ".cs"),
                targetFramework,
                targetPlatform)
            .WriteTo(tree.Combine(outputDirectory));

    private static AssemblyDiscoveryResult Discover(
        ProjectTree tree,
        AnalysisScope scope,
        LayoutHints? hints = null) =>
        AssemblyDiscovery.Discover(scope, tree.Root, tree.Root, hints ?? LayoutHints.None);

    // ---- The four relocations -------------------------------------------------------------

    [Theory]
    // The SDK's default.
    [InlineData("src/Core/bin/Debug/net10.0", "src/Tests/bin/Debug/net10.0")]
    // What `dotnet build -o` produces: flat, because global properties cannot be reassigned
    // during evaluation, so no target-framework segment is appended.
    [InlineData("out", "out")]
    // Artifacts output, where a single-targeted project gets no target-framework segment.
    [InlineData("artifacts/bin/Core/debug", "artifacts/bin/Tests/debug")]
    // And a shape nothing produces, to make the point that no path is ever computed.
    [InlineData("somewhere/else/entirely", "somewhere/else/entirely/deeper")]
    public async Task The_same_output_resolves_from_any_layout(string coreOutput, string testsOutput)
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        Emit(tree, "Core", CoreSource, coreOutput);
        Emit(tree, "Tests", TestsSource, testsOutput);

        var result = Discover(tree, scope);

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(["Core", "Tests"], result.Instances.Select(instance => instance.Name).Order(StringComparer.Ordinal));
    }

    // ---- Target frameworks -----------------------------------------------------------------

    [Fact]
    public async Task A_multi_targeted_project_resolves_to_several_instances()
    {
        var (tree, scope) = await SolutionAsync("net8.0;net10.0");
        using var _ = tree;

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net8.0", ".NETCoreApp,Version=v8.0");
        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0", ".NETCoreApp,Version=v10.0");
        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        Assert.True(result.Succeeded, result.Message);

        // Distinguished by TargetFrameworkAttribute, not by the directory they sit in.
        Assert.Equal(
            ["net10.0", "net8.0"],
            result.Instances
                .Where(instance => instance.Name == "Core")
                .Select(instance => instance.Expected.TargetFramework)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_platform_suffixed_framework_is_a_second_instance_not_an_ambiguity()
    {
        var (tree, scope) = await SolutionAsync("net10.0;net10.0-windows");
        using var _ = tree;

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0");
        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0-windows", targetPlatform: "Windows7.0");
        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        // TargetPlatformAttribute is what keeps these apart, and is why the ambiguity error
        // does not misfire here.
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.Instances.Count(instance => instance.Name == "Core"));
    }

    [Fact]
    public async Task Output_for_an_abandoned_target_framework_is_ignored()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0");
        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net8.0", ".NETCoreApp,Version=v8.0");
        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        // Its TargetFrameworkAttribute matches no expected instance, so it is not a second
        // candidate and not an error.
        Assert.True(result.Succeeded, result.Message);
        Assert.Single(result.Instances, instance => instance.Name == "Core");
    }

    // ---- Copies, and genuinely ambiguous output --------------------------------------------

    [Fact]
    public async Task The_same_build_copied_into_a_consumers_output_is_one_instance()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        var core = Compiled.Assembly("Core", CoreSource, tree.Combine("src", "Core", "Core.cs"));

        // Exactly what the build does: a project reference copies its output beside the
        // consumer's. Both files are the same build, and one module version id says so.
        core.WriteTo(tree.Combine("src/Core/bin/Debug/net10.0"));
        core.WriteTo(tree.Combine("src/Tests/bin/Debug/net10.0"));

        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        Assert.True(result.Succeeded, result.Message);

        // And the copy under the project's own directory is the one reported, because that is
        // the path a reader recognises.
        var instance = Assert.Single(result.Instances, candidate => candidate.Name == "Core");
        Assert.Contains(Path.Combine("Core", "bin"), instance.Assembly.Path);
    }

    [Fact]
    public async Task Debug_and_Release_both_present_is_exit_3_naming_the_option_that_settles_it()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0");
        Emit(tree, "Core", CoreSource, "src/Core/bin/Release/net10.0");
        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        // Guessing was rejected: newest-mtime silently picks a stale Release build over a
        // fresh Debug one about as often as not, turning a loud stop into under-selection.
        Assert.False(result.Succeeded);
        Assert.Equal(ExitCode.AssemblyDiscoveryFailed, result.ExitCode);
        Assert.Contains("Debug", result.Message);
        Assert.Contains("Release", result.Message);
        Assert.Contains("-c", result.Message);
    }

    [Fact]
    public async Task The_configuration_hint_breaks_that_ambiguity()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0");
        Emit(tree, "Core", CoreSource, "src/Core/bin/Release/net10.0");
        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Release/net10.0");

        var result = Discover(tree, scope, new LayoutHints("Release", null, null));

        Assert.True(result.Succeeded, result.Message);
        Assert.Contains("Release", Assert.Single(result.Instances, i => i.Name == "Core").Assembly.Path);
    }

    [Fact]
    public async Task The_output_hint_breaks_it_too()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        Emit(tree, "Core", CoreSource, "first/out");
        Emit(tree, "Core", CoreSource, "second/out");
        Emit(tree, "Tests", TestsSource, "second/out");

        var result = Discover(tree, scope, new LayoutHints(null, tree.Combine("second"), null));

        Assert.True(result.Succeeded, result.Message);
    }

    // ---- Errors ------------------------------------------------------------------------------

    [Fact]
    public async Task A_missing_expected_assembly_is_exit_3()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        // Absence is unambiguous here in a way it is not for a predicted path: the tree was
        // searched.
        Assert.False(result.Succeeded);
        Assert.Equal(ExitCode.AssemblyDiscoveryFailed, result.ExitCode);
        Assert.Contains("Core", result.Message);
    }

    [Fact]
    public async Task A_third_party_assembly_with_a_first_party_looking_name_is_not_first_party()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        // Right name, right target framework, and its symbols sit beside it — but its source
        // is outside the working tree. `.pdb` is an AllowedReferenceRelatedFileExtension, so
        // symbols beside an assembly prove nothing whatsoever.
        Compiled
            .Assembly("Core", CoreSource, "/somewhere/else/Widget.cs")
            .WriteTo(tree.Combine("src/Core/bin/Debug/net10.0"));

        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        Assert.False(result.Succeeded);
        Assert.Contains("not first-party", result.Message);
    }

    [Fact]
    public async Task Output_under_obj_is_never_a_candidate()
    {
        var (tree, scope) = await SolutionAsync();
        using var _ = tree;

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0");

        // The reference assembly MSBuild writes under obj/ would otherwise double every
        // candidate and make the ambiguity error fire on every run.
        Emit(tree, "Core", CoreSource, "src/Core/obj/Debug/net10.0/ref");

        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = Discover(tree, scope);

        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task A_generator_project_is_not_a_missing_assembly()
    {
        using var tree = ProjectTree.Create();

        var generator = tree.AddProject("Generator", targetFrameworks: "netstandard2.0");
        var core = tree.AddProject(
            "Core",
            targetFrameworks: "net10.0",
            decoratedReferences: [(generator, """ReferenceOutputAssembly="false" OutputItemType="Analyzer" """)]);
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        var discovery = TargetDiscovery.Discover(tree.AddSlnx("Solution", generator, core, tests));
        var scope = await AnalysisScopeResolver.ResolveAsync(discovery.Target!, Token);

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0");
        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = AssemblyDiscovery.Discover(scope.Scope!, tree.Root, tree.Root, LayoutHints.None);

        // It produces no output assembly where Reach looks, so it was never expected — and a
        // wrong exclusion in scope resolution would be a false error right here.
        Assert.True(result.Succeeded, result.Message);
        Assert.DoesNotContain(result.Instances, instance => instance.Name == "Generator");
    }

    [Fact]
    public async Task An_assembly_named_by_an_AssemblyName_override_is_found_under_that_name()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net10.0", assemblyName: "Contoso.Core");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        var discovery = TargetDiscovery.Discover(tree.AddSlnx("Solution", core, tests));
        var scope = await AnalysisScopeResolver.ResolveAsync(discovery.Target!, Token);

        Emit(tree, "Core", CoreSource, "src/Core/bin/Debug/net10.0", assemblyName: "Contoso.Core");
        Emit(tree, "Tests", TestsSource, "src/Tests/bin/Debug/net10.0");

        var result = AssemblyDiscovery.Discover(scope.Scope!, tree.Root, tree.Root, LayoutHints.None);

        Assert.True(result.Succeeded, result.Message);
    }
}
