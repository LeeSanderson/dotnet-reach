using Reach.Projects;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Projects;

/// <summary>
/// Analysis scope is the union of the transitive project closures of the test projects, and
/// nothing about the change ever narrows it.
/// </summary>
public class AnalysisScopeTests
{
    private const string TestPackage = "xunit.v3";

    private static Task<AnalysisScopeResult> Resolve(string target)
    {
        var discovery = TargetDiscovery.Discover(target);

        Assert.True(discovery.Discovered, discovery.Message);
        return AnalysisScopeResolver.ResolveAsync(discovery.Target!, TestContext.Current.CancellationToken);
    }

    private static string[] NamesIn(IEnumerable<ProjectFile> projects) =>
        [.. projects.Select(project => project.Name).Order(StringComparer.Ordinal)];

    [Fact]
    public async Task Sln_and_slnx_parse_to_the_same_project_list()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        var fromSlnx = await Resolve(tree.AddSlnx("Via-slnx", core, tests));
        var fromSln = await Resolve(tree.AddSln("Via-sln", core, tests));

        Assert.Equal(NamesIn(fromSlnx.Scope!.Projects), NamesIn(fromSln.Scope!.Projects));
        Assert.Equal(["Core", "Tests"], NamesIn(fromSlnx.Scope.Projects));
    }

    [Fact]
    public async Task The_closure_is_transitive_and_a_diamond_appears_once()
    {
        using var tree = ProjectTree.Create();

        //        Tests
        //          |
        //        Top
        //       /    \
        //   Left      Right
        //       \    /
        //        Bottom
        var bottom = tree.AddProject("Bottom", targetFrameworks: "net10.0");
        var left = tree.AddProject("Left", [bottom], targetFrameworks: "net10.0");
        var right = tree.AddProject("Right", [bottom], targetFrameworks: "net10.0");
        var top = tree.AddProject("Top", [left, right], targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [top], [TestPackage], "net10.0");

        var result = await Resolve(tree.AddSlnx("Diamond", bottom, left, right, top, tests));

        Assert.Equal(["Bottom", "Left", "Right", "Tests", "Top"], NamesIn(result.Scope!.Projects));
        Assert.Single(result.Scope.Projects, project => project.Name == "Bottom");
    }

    [Fact]
    public async Task Scope_is_never_narrowed_to_the_dependents_of_a_changed_project()
    {
        using var tree = ProjectTree.Create();

        // ADR-0002's shape. Caller references Contracts, never Implementation, so Caller is
        // not a dependent of Implementation — and narrowing to dependents would drop it
        // despite a change in Implementation reaching it at runtime.
        var contracts = tree.AddProject("Contracts", targetFrameworks: "net10.0");
        var implementation = tree.AddProject("Implementation", [contracts], targetFrameworks: "net10.0");
        var caller = tree.AddProject("Caller", [contracts], targetFrameworks: "net10.0");
        var composition = tree.AddProject("Composition", [caller, implementation], targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [composition], [TestPackage], "net10.0");

        var result = await Resolve(
            tree.AddSlnx("Wiring", contracts, implementation, caller, composition, tests));

        Assert.Equal(
            ["Caller", "Composition", "Contracts", "Implementation", "Tests"],
            NamesIn(result.Scope!.Projects));

        // The assertion that matters: nothing in the resolver's signature or result lets a
        // caller hand it a changed project to narrow by.
        Assert.Contains(result.Scope.Projects, project => project.Name == "Caller");
    }

    [Fact]
    public async Task A_project_referenced_with_ReferenceOutputAssembly_false_is_still_in_the_closure()
    {
        using var tree = ProjectTree.Create();

        var generator = tree.AddProject("Generator", targetFrameworks: "netstandard2.0");
        var core = tree.AddProject(
            "Core",
            targetFrameworks: "net10.0",
            decoratedReferences:
            [
                (generator, """ReferenceOutputAssembly="false" OutputItemType="Analyzer" """)
            ]);
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        var result = await Resolve(tree.AddSlnx("WithGenerator", generator, core, tests));

        // Such a reference expresses build order rather than runtime executability, so
        // including it in the closure errs wide — the safe direction.
        Assert.Contains(result.Scope!.Projects, project => project.Name == "Generator");

        // But it puts no assembly where Reach looks, so it is not expected to be found there.
        // A wrong inclusion here becomes a false exit 3 in assembly discovery.
        Assert.DoesNotContain(
            result.Scope.ExpectedAssemblies,
            instance => instance.Project.Name == "Generator");
    }

    [Fact]
    public async Task A_multi_targeted_project_contributes_one_assembly_instance_per_framework()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net8.0;net10.0");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        var result = await Resolve(tree.AddSlnx("Multi", core, tests));

        Assert.Equal(
            ["net10.0", "net8.0"],
            result.Scope!.ExpectedAssemblies
                .Where(instance => instance.Project.Name == "Core")
                .Select(instance => instance.TargetFramework)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task An_AssemblyName_override_is_read_including_from_Directory_Build_props()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net10.0", assemblyName: "Contoso.Core");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        // The framework arrives from Directory.Build.props, as it does in Reach's own tree.
        tree.WriteFile(
            "src/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var result = await Resolve(tree.AddSlnx("Named", core, tests));

        var instance = Assert.Single(
            result.Scope!.ExpectedAssemblies,
            candidate => candidate.Project.Name == "Core");

        Assert.Equal("Contoso.Core", instance.Project.AssemblyName);
        Assert.Equal("net10.0", instance.TargetFramework);
    }

    [Fact]
    public async Task A_target_framework_inherited_from_Directory_Build_props_is_read()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core");
        var tests = tree.AddProject("Tests", [core], [TestPackage]);

        tree.WriteFile(
            "src/Directory.Build.props",
            """
            <Project>
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);

        var result = await Resolve(tree.AddSlnx("Inherited", core, tests));

        Assert.All(
            result.Scope!.ExpectedAssemblies,
            instance => Assert.Equal("net10.0", instance.TargetFramework));
    }

    [Fact]
    public async Task A_project_with_no_test_project_in_its_closure_is_exit_1_naming_it()
    {
        using var tree = ProjectTree.Create();

        var worker = tree.AddProject("Worker", targetFrameworks: "net10.0");

        var result = await Resolve(worker);

        // A mis-invocation, not nothing-selected: "no tests affected" would be a lie a
        // pipeline believes.
        Assert.False(result.Resolved);
        Assert.Equal(ExitCode.UsageError, result.ExitCode);
        Assert.Contains("Worker.csproj", result.Message);
    }

    [Fact]
    public async Task A_solution_with_no_test_project_anywhere_is_exit_1()
    {
        using var tree = ProjectTree.Create();

        var a = tree.AddProject("A", targetFrameworks: "net10.0");
        var b = tree.AddProject("B", [a], targetFrameworks: "net10.0");

        var result = await Resolve(tree.AddSlnx("NoTests", a, b));

        Assert.False(result.Resolved);
    }

    [Fact]
    public async Task A_project_outside_every_solution_emits_the_scope_notice_and_continues()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        // A solution exists, but does not list the project Reach was pointed at.
        tree.AddSlnx("Partial", core);

        var result = await Resolve(tests);

        Assert.True(result.Resolved, result.Message);
        Assert.Contains(result.Notices, notice => notice.Code == NoticeCodes.ProjectOutsideEverySolution);
        Assert.Equal(NoticeKind.Scope, Assert.Single(result.Notices).Kind);
    }

    [Fact]
    public async Task A_project_that_a_solution_above_it_lists_emits_no_scope_notice()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        tree.AddSlnx("Whole", core, tests);

        var result = await Resolve(tests);

        Assert.True(result.Resolved, result.Message);
        Assert.Empty(result.Notices);

        // Scope is still that project's own closure, not the whole solution's.
        Assert.Equal(["Core", "Tests"], NamesIn(result.Scope!.Projects));
    }

    [Fact]
    public async Task A_project_outside_the_solution_is_still_followed_into_the_closure()
    {
        using var tree = ProjectTree.Create();

        var outside = tree.AddProject("Outside", targetFrameworks: "net10.0");
        var core = tree.AddProject("Core", [outside], targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        // The solution supplies the expected project list, never the closure.
        var result = await Resolve(tree.AddSlnx("Partial", core, tests));

        Assert.Contains(result.Scope!.Projects, project => project.Name == "Outside");
    }

    [Fact]
    public async Task Only_projects_reachable_from_a_test_project_are_in_scope()
    {
        using var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net10.0");
        var unreachable = tree.AddProject("Unreachable", targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");

        var result = await Resolve(tree.AddSlnx("Some", core, unreachable, tests));

        // Code outside every test closure cannot execute in any test process.
        Assert.Equal(["Core", "Tests"], NamesIn(result.Scope!.Projects));
    }
}
