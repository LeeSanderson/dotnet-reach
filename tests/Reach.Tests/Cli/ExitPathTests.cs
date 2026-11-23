using System.Globalization;
using System.Text.Json;
using Reach.Cli;
using Reach.Output;
using Reach.Processes;
using Reach.Tests.Assemblies;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Cli;

/// <summary>
/// What Reach writes, and leaves out, on each exit path that fails after usage has been
/// checked. Every one of them writes a report: a tool that writes nothing when it fails is one
/// you debug by re-running it with more flags, on CI.
/// </summary>
/// <remarks>
/// Exit 2 here is only the report-on-failure claim. That a broken build is exit 2 end to end is
/// an acceptance row's job.
/// </remarks>
public sealed class ExitPathTests : IDisposable
{
    private const string Before = "namespace N; public class Widget { public int Spin() => 1; }";
    private const string After = "namespace N; public class Widget { public int Spin() => 42; }";
    private const string TestsSource = "namespace N.Tests; public class T { public void M() { } }";

    private readonly StringWriter _standardOutput = new();
    private readonly StringWriter _standardError = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private Task<int> RunAsync(string workingDirectory, IProcessRunner processRunner, params string[] arguments) =>
        new ReachCli(processRunner).RunAsync(
            arguments,
            workingDirectory,
            _standardOutput,
            _standardError,
            _ => null,
            outputIsRedirected: false,
            Token);

    private static int Numeric(ExitCode code) => Convert.ToInt32(code, CultureInfo.InvariantCulture);

    // ---- Exit 70 ------------------------------------------------------------------------------

    [Fact]
    public async Task A_pipeline_that_throws_is_exit_70()
    {
        using var tree = CommandLineTests.SolutionWithTests();

        // A runner with nothing scripted throws on the first git call, which is the pipeline
        // failing in a way no phase anticipated.
        var exitCode = await RunAsync(tree.Root, new ScriptedProcessRunner(), "select", "--base", "main");

        Assert.Equal(Numeric(ExitCode.InternalError), exitCode);
        Assert.Contains("internal error", _standardError.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Exit_70_writes_a_failed_report_with_no_summary()
    {
        using var tree = CommandLineTests.SolutionWithTests();

        await RunAsync(tree.Root, new ScriptedProcessRunner(), "select", "--base", "main");

        using var report = await ReportAsync(tree.Root);

        AssertFailedWithNoSummary(report);
    }

    // ---- Exit 2 -------------------------------------------------------------------------------

    [Fact]
    public async Task Exit_2_writes_a_failed_report_with_no_summary()
    {
        using var repository = CommandLineTests.SolutionRepository();

        // git is real; the build fails.
        var runner = new SplitRunner(
            new ScriptedProcessRunner().Fails(1, "error CS1002: ; expected", "build"),
            new ProcessRunner());

        var exitCode = await RunAsync(repository.Path, runner, "select", "--base", "main");

        Assert.Equal(Numeric(ExitCode.BuildFailed), exitCode);

        using var report = await ReportAsync(repository.Path);

        AssertFailedWithNoSummary(report);
    }

    // ---- Exit 4 -------------------------------------------------------------------------------

    [Fact]
    public async Task Exit_4_writes_a_failed_report_with_no_summary()
    {
        using var tree = CommandLineTests.SolutionWithTests();

        // No --base, no CI variables, and no git repository, so resolution stops.
        var exitCode = await RunAsync(tree.Root, new ProcessRunner(), "select");

        Assert.Equal(Numeric(ExitCode.BaselineUnresolvable), exitCode);

        using var report = await ReportAsync(tree.Root);

        AssertFailedWithNoSummary(report);
    }

    [Fact]
    public async Task Exit_4_writes_no_baseline_into_the_envelope()
    {
        using var tree = CommandLineTests.SolutionWithTests();

        await RunAsync(tree.Root, new ProcessRunner(), "select");

        using var report = await ReportAsync(tree.Root);

        Assert.False(Envelope(report).TryGetProperty("baseline", out _));
    }

    // ---- Exit 5 -------------------------------------------------------------------------------

    [Fact]
    public async Task Exit_5_writes_a_failed_report_with_no_summary()
    {
        using var repository = CommandLineTests.SolutionRepository();

        var sourcePath = repository.WriteFile("src/Core/Widget.cs", Before);
        repository.WriteFile("src/Tests/Tests.cs", TestsSource);
        repository.Commit("widget");

        Compile(repository, sourcePath, Before);

        // The binaries now belong to a different revision of the file.
        await File.WriteAllTextAsync(sourcePath, After, Token);

        var exitCode = await RunAsync(repository.Path, new ProcessRunner(), "select", "--base", "main", "--no-build");

        Assert.Equal(Numeric(ExitCode.CorrespondenceFailed), exitCode);

        using var report = await ReportAsync(repository.Path);

        AssertFailedWithNoSummary(report);
    }

    // ---- envelope.head ------------------------------------------------------------------------

    [Fact]
    public async Task A_head_git_cannot_read_is_absent_from_the_envelope()
    {
        using var repository = CommandLineTests.SolutionRepository();

        await RunAsync(repository.Path, new UnreadableHead(new ProcessRunner()), "select", "--base", "main", "--no-build");

        using var report = await ReportAsync(repository.Path);

        // The baseline still resolved, so the envelope is otherwise populated.
        Assert.True(Envelope(report).TryGetProperty("baseline", out _));
        Assert.False(Envelope(report).TryGetProperty("head", out _));
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private static void AssertFailedWithNoSummary(JsonDocument report)
    {
        Assert.Equal("failed", report.RootElement.GetProperty("outcome").GetString());

        // Every one of these runs stopped before rendering, so there is nothing to measure.
        Assert.False(report.RootElement.TryGetProperty("summary", out _));
    }

    private static JsonElement Envelope(JsonDocument report) => report.RootElement.GetProperty("envelope");

    private static async Task<JsonDocument> ReportAsync(string workingDirectory)
    {
        var path = Path.Combine(workingDirectory, ReachDirectory.DefaultName, "report.json");

        Assert.True(File.Exists(path), $"no report at {path}");

        return JsonDocument.Parse(await File.ReadAllTextAsync(path, Token));
    }

    private static void Compile(TempRepository repository, string sourcePath, string source)
    {
        File.WriteAllText(sourcePath, source);

        Compiled
            .Assembly("Core", source, sourcePath)
            .WriteTo(repository.Combine("src", "Core", "bin", "Debug", "net10.0"));

        Compiled
            .Assembly("Tests", TestsSource, repository.Combine("src", "Tests", "Tests.cs"))
            .WriteTo(repository.Combine("src", "Tests", "bin", "Debug", "net10.0"));
    }

    public void Dispose()
    {
        _standardOutput.Dispose();
        _standardError.Dispose();
    }

    /// <summary>
    /// Real git, except that <c>rev-parse HEAD</c> fails the way it does in a repository git
    /// cannot read.
    /// </summary>
    private sealed class UnreadableHead(IProcessRunner real) : IProcessRunner
    {
        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken) =>
            request.Arguments is [.., "rev-parse", "HEAD"]
                ? Task.FromResult(new ProcessResult(128, string.Empty, "fatal: ambiguous argument 'HEAD'"))
                : real.RunAsync(request, cancellationToken);
    }
}
