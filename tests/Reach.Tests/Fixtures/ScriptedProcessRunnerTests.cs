using Reach.Processes;

namespace Reach.Tests.Fixtures;

/// <summary>
/// The fake every later ticket's unit tests depend on, so it is worth testing on its own:
/// a fake that quietly matches the wrong script turns real assertions into decoration.
/// </summary>
public class ScriptedProcessRunnerTests
{
    private readonly ScriptedProcessRunner runner = new();

    private Task<ProcessResult> Run(params string[] arguments) =>
        runner.RunAsync(
            new ProcessRequest("git", arguments, "/work"),
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task Scripts_a_failure()
    {
        runner.Fails(128, "fatal: bad revision\n", "rev-parse");

        var result = await Run("rev-parse", "nope");

        Assert.Equal(128, result.ExitCode);
        Assert.Equal("fatal: bad revision\n", result.StandardError);
        Assert.Empty(result.StandardOutput);
    }

    [Fact]
    public async Task Scripts_an_empty_output()
    {
        runner.Succeeds("", "diff");

        var result = await Run("diff", "--name-status", "abc123");

        Assert.True(result.Succeeded);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public async Task Scripts_a_multi_line_output()
    {
        runner.Succeeds("one\ntwo\nthree\n", "ls-files");

        var result = await Run("ls-files", "--others");

        Assert.Equal(["one", "two", "three"], result.Lines);
    }

    [Fact]
    public async Task Records_the_argument_vector_it_was_asked_for()
    {
        runner.Succeeds("", "diff");

        await Run("-c", "core.quotePath=false", "diff", "--no-renames");

        var request = Assert.Single(runner.Requests);
        Assert.Equal(["-c", "core.quotePath=false", "diff", "--no-renames"], request.Arguments);
    }

    [Fact]
    public async Task Matches_scripts_in_the_order_they_were_added()
    {
        runner.Succeeds("first\n", "rev-parse");
        runner.Succeeds("second\n", "rev-parse", "--is-shallow-repository");

        Assert.Equal("first", (await Run("rev-parse", "HEAD")).Value);
        Assert.Equal("first", (await Run("rev-parse", "--is-shallow-repository")).Value);
    }

    [Fact]
    public async Task Refuses_an_argument_vector_it_was_not_scripted_for()
    {
        runner.Succeeds("", "diff");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => Run("merge-base", "HEAD"));

        Assert.Contains("merge-base HEAD", thrown.Message);
    }

    [Fact]
    public async Task A_script_matches_a_vector_that_carries_extra_arguments_around_it()
    {
        runner.Succeeds("abc123\n", "merge-base", "HEAD");

        var result = await Run("-c", "core.quotePath=false", "merge-base", "HEAD", "origin/main");

        Assert.Equal("abc123", result.Value);
    }
}
