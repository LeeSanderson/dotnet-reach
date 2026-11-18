using Reach.Processes;

namespace Reach.Tests.Fixtures;

/// <summary>
/// The fake every later ticket's unit tests depend on, so it is worth testing on its own:
/// a fake that quietly matches the wrong script turns real assertions into decoration.
/// </summary>
public class ScriptedProcessRunnerTests
{
    private readonly ScriptedProcessRunner _runner = new();

    private Task<ProcessResult> RunAsync(params string[] arguments) =>
        _runner.RunAsync(
            new ProcessRequest("git", arguments, "/work"),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Scripts_a_failure()
    {
        _runner.Fails(128, "fatal: bad revision\n", "rev-parse");

        var result = await RunAsync("rev-parse", "nope");

        Assert.Equal(128, result.ExitCode);
        Assert.Equal("fatal: bad revision\n", result.StandardError);
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public async Task Scripts_an_empty_output()
    {
        _runner.Succeeds("", "diff");

        var result = await RunAsync("diff", "--name-status", "abc123");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task Scripts_a_multi_line_output()
    {
        _runner.Succeeds("one\ntwo\nthree\n", "ls-files");

        var result = await RunAsync("ls-files", "--others");

        Assert.Equal(["one", "two", "three"], result.Lines);
    }

    [Fact]
    public async Task Records_the_argument_vector_it_was_asked_for()
    {
        _runner.Succeeds("", "diff");

        await RunAsync("-c", "core.quotePath=false", "diff", "--no-renames");

        var request = Assert.Single(_runner.Requests);
        Assert.Equal(["-c", "core.quotePath=false", "diff", "--no-renames"], request.Arguments);
    }

    [Fact]
    public async Task The_most_recently_scripted_answer_wins()
    {
        _runner.Succeeds("default\n", "rev-parse");
        _runner.Succeeds("overridden\n", "rev-parse");

        // So a test can override a fixture's default without rebuilding it.
        Assert.Equal("overridden", (await RunAsync("rev-parse", "HEAD")).Value);
    }

    [Fact]
    public async Task Refuses_an_argument_vector_it_was_not_scripted_for()
    {
        _runner.Succeeds("", "diff");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => RunAsync("merge-base", "HEAD"));

        Assert.Contains("merge-base HEAD", thrown.Message);
    }

    [Fact]
    public async Task A_script_matches_a_vector_that_carries_extra_arguments_around_it()
    {
        _runner.Succeeds("abc123\n", "merge-base", "HEAD");

        var result = await RunAsync("-c", "core.quotePath=false", "merge-base", "HEAD", "origin/main");

        Assert.Equal("abc123", result.Value);
    }
}
