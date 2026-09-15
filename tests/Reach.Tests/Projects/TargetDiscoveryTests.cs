using Reach.Projects;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Projects;

/// <summary>
/// One test per row of the discovery table. Every ambiguity is an error rather than a choice:
/// a monorepo with six solutions would get a coin flip, and the wrong solution silently
/// produces a wrong analysis scope.
/// </summary>
public class TargetDiscoveryTests
{
    [Fact]
    public void Exactly_one_solution_wins_whatever_projects_sit_beside_it()
    {
        using var tree = ProjectTree.Create();

        var a = tree.AddProject("A");
        tree.AddProject("B");
        tree.AddProject("C");
        var solution = tree.AddSlnx("Only", a);

        var result = TargetDiscovery.Discover(tree.Root);

        // MSBuild would compare base names and error. For Reach a solution and a project are
        // two different analysis scopes, so picking the project narrows — silently.
        Assert.True(result.Discovered, result.Message);
        Assert.Equal(TargetKind.Solution, result.Target!.Kind);
        Assert.Equal(Paths.Normalise(solution), result.Target.Path);
    }

    [Fact]
    public void More_than_one_solution_is_exit_1_naming_what_was_found()
    {
        using var tree = ProjectTree.Create();

        var a = tree.AddProject("A");
        tree.AddSlnx("First", a);
        tree.AddSln("Second", a);

        var result = TargetDiscovery.Discover(tree.Root);

        Assert.False(result.Discovered);
        Assert.Equal(ExitCode.UsageError, result.ExitCode);
        Assert.Contains("First.slnx", result.Message);
        Assert.Contains("Second.sln", result.Message);
    }

    [Fact]
    public void No_solution_and_exactly_one_project_is_that_project()
    {
        using var tree = ProjectTree.Create();

        var only = tree.AddProject("Only");
        var directory = Path.GetDirectoryName(only)!;

        var result = TargetDiscovery.Discover(directory);

        Assert.True(result.Discovered, result.Message);
        Assert.Equal(TargetKind.Project, result.Target!.Kind);
        Assert.Equal(Paths.Normalise(only), result.Target.Path);
    }

    [Fact]
    public void No_solution_and_more_than_one_project_is_exit_1_naming_what_was_found()
    {
        using var tree = ProjectTree.Create();

        tree.WriteFile("both/First.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        tree.WriteFile("both/Second.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var result = TargetDiscovery.Discover(tree.Combine("both"));

        Assert.False(result.Discovered);
        Assert.Contains("First.csproj", result.Message);
        Assert.Contains("Second.csproj", result.Message);
    }

    [Fact]
    public void A_solution_filter_is_exit_1_naming_the_underlying_solution()
    {
        using var tree = ProjectTree.Create();

        var a = tree.AddProject("A");
        var b = tree.AddProject("B");
        var solution = tree.AddSln("Whole", a, b);
        var filter = tree.AddSlnf("Subset", solution, a);

        var result = TargetDiscovery.Discover(filter);

        // A filter declares a subset, and honouring it shrinks analysis scope by exactly the
        // mechanism ADR-0002 forbids. Rejected rather than expanded.
        Assert.False(result.Discovered);
        Assert.Contains("Whole.sln", result.Message);
    }

    [Fact]
    public void A_solution_filter_alone_in_a_directory_is_still_exit_1()
    {
        using var tree = ProjectTree.Create();

        var a = tree.AddProject("A");
        var solution = tree.Combine("Elsewhere.sln");
        tree.AddSlnf("Subset", solution, a);

        var result = TargetDiscovery.Discover(tree.Root);

        Assert.False(result.Discovered);
        Assert.Contains("Elsewhere.sln", result.Message);
    }

    [Fact]
    public void A_solution_beside_a_filter_still_wins()
    {
        using var tree = ProjectTree.Create();

        var a = tree.AddProject("A");
        var solution = tree.AddSlnx("Whole", a);
        tree.AddSlnf("Subset", solution, a);

        var result = TargetDiscovery.Discover(tree.Root);

        // The filter was never chosen, so it states nothing. Refusing here would break the
        // common layout where a convenience filter sits beside the solution it filters.
        Assert.True(result.Discovered, result.Message);
        Assert.Equal(Paths.Normalise(solution), result.Target!.Path);
    }

    [Fact]
    public void A_named_solution_is_taken_as_given()
    {
        using var tree = ProjectTree.Create();

        var a = tree.AddProject("A");
        var first = tree.AddSlnx("First", a);
        tree.AddSlnx("Second", a);

        var result = TargetDiscovery.Discover(first);

        Assert.True(result.Discovered, result.Message);
        Assert.Equal(Paths.Normalise(first), result.Target!.Path);
    }

    [Fact]
    public void Discovery_never_recurses()
    {
        using var tree = ProjectTree.Create();

        // The only solution and the only project both sit a directory down. Finding either
        // would be the coin flip; the root holds nothing, so the root is the answer.
        tree.AddProject("A");
        tree.WriteFile("nested/Deep.slnx", "<Solution />");

        var result = TargetDiscovery.Discover(tree.Root);

        Assert.False(result.Discovered);
        Assert.DoesNotContain("Deep.slnx", result.Message);
        Assert.DoesNotContain("A.csproj", result.Message);
        Assert.Contains("no solution and no project", result.Message);
    }

    [Fact]
    public void A_path_that_is_neither_a_file_nor_a_directory_is_exit_1()
    {
        using var tree = ProjectTree.Create();

        var result = TargetDiscovery.Discover(tree.Combine("nothing-here.slnx"));

        Assert.False(result.Discovered);
        Assert.Equal(ExitCode.UsageError, result.ExitCode);
    }
}
