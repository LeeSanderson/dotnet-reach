using Reach.Git;
using Reach.Processes;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Git;

/// <summary>
/// The argument vector each git operation issues. These are assertions about an exact argv,
/// because a wrong flag here is not a crash: <c>--no-renames</c> going missing hands ticket
/// 06's matching over to git's similarity threshold, and <c>--exclude-standard</c> going
/// missing widens every project on every run.
/// </summary>
public class GitAdapterTests
{
    private readonly ScriptedProcessRunner runner = new();

    private GitAdapter Adapter => new(runner, "/work");

    [Fact]
    public async Task Resolves_a_merge_base_against_HEAD()
    {
        runner.Succeeds("abc123\n", "merge-base");

        var result = await Adapter.MergeBaseAsync("origin/main", TestContext.Current.CancellationToken);

        Assert.Equal("abc123", result.Value);
        AssertArgv("git", "-c", "core.quotePath=false", "merge-base", "HEAD", "origin/main");
    }

    [Fact]
    public async Task Resolves_a_reference_to_a_sha()
    {
        runner.Succeeds("deadbeef\n", "rev-parse");

        await Adapter.RevParseAsync("HEAD~1", TestContext.Current.CancellationToken);

        AssertArgv("git", "-c", "core.quotePath=false", "rev-parse", "HEAD~1");
    }

    [Fact]
    public async Task Detects_a_shallow_clone()
    {
        runner.Succeeds("true\n", "--is-shallow-repository");

        var result = await Adapter.IsShallowCloneAsync(TestContext.Current.CancellationToken);

        Assert.Equal("true", result.Value);
        AssertArgv("git", "-c", "core.quotePath=false", "rev-parse", "--is-shallow-repository");
    }

    [Fact]
    public async Task Asks_for_the_changed_set_with_renames_switched_off()
    {
        runner.Succeeds("M\tsrc/A.cs\nA\tsrc/B.cs\n", "diff");

        var result = await Adapter.ChangedPathsAsync("abc123", TestContext.Current.CancellationToken);

        Assert.Equal(["M\tsrc/A.cs", "A\tsrc/B.cs"], result.Lines);
        AssertArgv("git", "-c", "core.quotePath=false", "diff", "--no-renames", "--name-status", "abc123");
    }

    [Fact]
    public async Task Asks_for_untracked_files_excluding_ignored_ones()
    {
        runner.Succeeds("src/New.cs\n", "ls-files");

        await Adapter.UntrackedPathsAsync(TestContext.Current.CancellationToken);

        AssertArgv("git", "-c", "core.quotePath=false", "ls-files", "--others", "--exclude-standard");
    }

    [Fact]
    public async Task Reads_a_file_at_the_baseline()
    {
        runner.Succeeds("class A;\n", "show");

        await Adapter.FileAtBaselineAsync("abc123", "src/A.cs", TestContext.Current.CancellationToken);

        AssertArgv("git", "-c", "core.quotePath=false", "show", "abc123:src/A.cs");
    }

    [Fact]
    public async Task Resolves_the_repository_root()
    {
        runner.Succeeds("/work\n", "--show-toplevel");

        await Adapter.RepositoryRootAsync(TestContext.Current.CancellationToken);

        AssertArgv("git", "-c", "core.quotePath=false", "rev-parse", "--show-toplevel");
    }

    [Fact]
    public async Task A_failure_reaches_the_caller_as_a_result()
    {
        runner.Fails(128, "fatal: not a git repository\n", "merge-base");

        var result = await Adapter.MergeBaseAsync("origin/main", TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(128, result.ExitCode);
        Assert.Contains("not a git repository", result.StandardError);
    }

    [Fact]
    public async Task Every_command_runs_in_the_directory_it_was_given()
    {
        runner.Succeeds("", "rev-parse");

        await Adapter.RevParseAsync("HEAD", TestContext.Current.CancellationToken);

        Assert.Equal("/work", Assert.Single(runner.Requests).WorkingDirectory);
    }

    private void AssertArgv(string executable, params string[] arguments)
    {
        var request = Assert.Single(runner.Requests);

        Assert.Equal(executable, request.Executable);
        Assert.Equal(arguments, request.Arguments);
    }
}
