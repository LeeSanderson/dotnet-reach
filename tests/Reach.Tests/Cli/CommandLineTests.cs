using Reach.Cli;
using Reach.Output;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Cli;

/// <summary>
/// The command line as a product surface: which invocations are refused, where each stream
/// goes, and what Reach writes into the one directory it owns.
/// </summary>
public class CommandLineTests
{
    private const string TestPackage = "xunit.v3";

    private readonly Dictionary<string, string?> environment = new(StringComparer.Ordinal);

    private readonly StringWriter standardOutput = new();
    private readonly StringWriter standardError = new();

    private Task<int> Run(string workingDirectory, params string[] arguments) =>
        ReachCli.RunAsync(
            arguments,
            workingDirectory,
            standardOutput,
            standardError,
            name => environment.GetValueOrDefault(name),
            outputIsRedirected: false,
            TestContext.Current.CancellationToken);

    private string Out => standardOutput.ToString();

    private string Error => standardError.ToString();

    // ---- The verb -----------------------------------------------------------------------

    [Fact]
    public async Task Bare_reach_prints_help_and_exits_1()
    {
        using var tree = ProjectTree.Create();

        var exitCode = await Run(tree.Root);

        // The default mode invokes dotnet build, so the one invocation a newcomer is
        // guaranteed to try must not trigger a multi-minute build and write files into their
        // repository — and a bare invocation writes no report, so exiting 0 would hand a
        // mis-wired pipeline "success" plus a missing report.
        Assert.Equal((int)ExitCode.UsageError, exitCode);
        Assert.Contains("select", Out + Error);
    }

    [Fact]
    public async Task Help_asked_for_explicitly_exits_0()
    {
        using var tree = ProjectTree.Create();

        var exitCode = await Run(tree.Root, "--help");

        Assert.Equal((int)ExitCode.Success, exitCode);
        Assert.Contains("select", Out);
    }

    [Fact]
    public async Task An_unknown_option_is_a_usage_error()
    {
        using var tree = ProjectTree.Create();

        Assert.Equal((int)ExitCode.UsageError, await Run(tree.Root, "select", "--bogus"));
    }

    [Fact]
    public async Task An_unknown_verbosity_level_is_a_usage_error()
    {
        using var tree = ProjectTree.Create();

        Assert.Equal((int)ExitCode.UsageError, await Run(tree.Root, "select", "-v", "loud"));
    }

    // ---- The -- boundary ----------------------------------------------------------------

    [Theory]
    [InlineData("-o", "--output")]
    [InlineData("--output", "--output")]
    [InlineData("--artifacts-path", "--artifacts-path")]
    [InlineData("-c", "--configuration")]
    [InlineData("--configuration", "--configuration")]
    public async Task A_layout_switch_after_the_boundary_is_exit_1_naming_the_Reach_option(
        string forwarded,
        string instead)
    {
        using var tree = ProjectTree.Create();

        var exitCode = await Run(tree.Root, "select", "--", forwarded, "somewhere");

        // A forwarded output switch changes the layout through a channel Reach never read.
        Assert.Equal((int)ExitCode.UsageError, exitCode);
        Assert.Contains(instead, Out + Error);
    }

    [Fact]
    public async Task A_layout_switch_written_with_an_equals_sign_is_refused_too()
    {
        using var tree = ProjectTree.Create();

        Assert.Equal(
            (int)ExitCode.UsageError,
            await Run(tree.Root, "select", "--", "--output=somewhere"));
    }

    [Fact]
    public void Other_tokens_after_the_boundary_reach_the_build_verbatim()
    {
        var (reach, build) = ForwardedBuildArguments.Split(
            ["select", "MySolution.slnx", "--base", "main", "--", "-p:Foo=1", "--no-restore"]);

        Assert.Equal(["select", "MySolution.slnx", "--base", "main"], reach);
        Assert.Equal(["-p:Foo=1", "--no-restore"], build);
    }

    [Fact]
    public void The_boundary_is_split_before_the_parser_sees_it()
    {
        // System.CommandLine's positional argument would otherwise swallow the first
        // forwarded token when no target was given, and '--no-restore' would become the
        // solution path.
        var (reach, build) = ForwardedBuildArguments.Split(["select", "--", "--no-restore"]);

        Assert.Equal(["select"], reach);
        Assert.Equal(["--no-restore"], build);
    }

    [Fact]
    public void An_argument_vector_with_no_boundary_forwards_nothing()
    {
        var (reach, build) = ForwardedBuildArguments.Split(["select", "--no-build"]);

        Assert.Equal(["select", "--no-build"], reach);
        Assert.Empty(build);
    }

    // ---- The directory Reach owns --------------------------------------------------------

    [Fact]
    public async Task A_usage_error_writes_nothing_into_the_directory_Reach_owns()
    {
        using var tree = ProjectTree.Create();

        var exitCode = await Run(tree.Root, "select", "--", "-o", "somewhere");

        // Exit 1 is the one case that writes no report.
        Assert.Equal((int)ExitCode.UsageError, exitCode);
        Assert.False(Directory.Exists(Path.Combine(tree.Root, ReachDirectory.DefaultName)));
    }

    [Fact]
    public async Task A_run_that_gets_past_usage_creates_the_directory_with_a_gitignore()
    {
        using var tree = SolutionWithTests();

        await Run(tree.Root, "select", "--base", "main");

        var gitignore = Path.Combine(tree.Root, ReachDirectory.DefaultName, ".gitignore");

        // So the first adopter to run Reach locally does not commit a report.
        Assert.True(File.Exists(gitignore));
        Assert.Equal("*", File.ReadAllText(gitignore).Trim());
    }

    [Fact]
    public void An_existing_gitignore_is_left_alone()
    {
        using var tree = ProjectTree.Create();

        var directory = ReachDirectory.For(tree.Root, null);
        directory.EnsureCreated();

        var gitignore = directory.Combine(".gitignore");
        File.WriteAllText(gitignore, "# edited by hand\n*.json\n");

        directory.EnsureCreated();

        // It may have been edited deliberately, and Reach rewriting it would be a surprise.
        Assert.Contains("edited by hand", File.ReadAllText(gitignore));
    }

    [Fact]
    public async Task Report_dir_relocates_the_whole_directory_including_side_car_files()
    {
        using var tree = SolutionWithTests();

        await Run(tree.Root, "select", "--base", "main", "--report-dir", "build/reach-output");

        var relocated = Path.Combine(tree.Root, "build", "reach-output");

        Assert.True(File.Exists(Path.Combine(relocated, ".gitignore")));
        Assert.False(Directory.Exists(Path.Combine(tree.Root, ReachDirectory.DefaultName)));

        // Side-car files follow the directory, not the report: --report moves the JSON alone.
        var directory = ReachDirectory.For(tree.Root, "build/reach-output");
        Assert.Equal(
            Path.Combine(relocated, "filter.rsp"),
            directory.Combine("filter.rsp"));
    }

    [Fact]
    public void The_report_defaults_into_the_directory_Reach_owns()
    {
        using var tree = ProjectTree.Create();

        var destination = ReportDestination.Resolve(
            null,
            ReachDirectory.For(tree.Root, null),
            tree.Root);

        Assert.Equal(
            Path.Combine(tree.Root, ReachDirectory.DefaultName, "report.json"),
            destination.Path);
    }

    [Fact]
    public void Report_moves_the_json_alone_and_leaves_the_directory_where_it_was()
    {
        using var tree = ProjectTree.Create();

        var directory = ReachDirectory.For(tree.Root, null);
        var destination = ReportDestination.Resolve("out/custom.json", directory, tree.Root);

        Assert.Equal(Path.Combine(tree.Root, "out", "custom.json"), destination.Path);
        Assert.Equal(Path.Combine(tree.Root, ReachDirectory.DefaultName), directory.Path);
    }

    // ---- Streams and colour ---------------------------------------------------------------

    [Fact]
    public void Report_dash_puts_the_json_on_stdout_and_moves_the_summary_to_stderr()
    {
        using var tree = ProjectTree.Create();

        var destination = ReportDestination.Resolve("-", ReachDirectory.For(tree.Root, null), tree.Root);
        var streams = Streams.For(destination, standardOutput, standardError, colour: false);

        Assert.True(destination.ToStandardOutput);
        Assert.Same(standardOutput, streams.Report);
        Assert.Same(standardError, streams.Summary);
    }

    [Fact]
    public void By_default_the_summary_is_on_stdout_and_nothing_else_is()
    {
        using var tree = ProjectTree.Create();

        var destination = ReportDestination.Resolve(null, ReachDirectory.For(tree.Root, null), tree.Root);
        var streams = Streams.For(destination, standardOutput, standardError, colour: false);

        Assert.Same(standardOutput, streams.Summary);
        Assert.Same(standardError, streams.Notices);
        Assert.Null(streams.Report);
    }

    [Fact]
    public void No_color_and_NO_COLOR_and_a_redirected_stdout_each_suppress_colour()
    {
        string? Empty(string name) => null;
        string? NoColor(string name) => name == "NO_COLOR" ? "1" : null;

        Assert.True(ColourSupport.Enabled(noColorOption: false, Empty, outputIsRedirected: false));

        Assert.False(ColourSupport.Enabled(noColorOption: true, Empty, outputIsRedirected: false));
        Assert.False(ColourSupport.Enabled(noColorOption: false, NoColor, outputIsRedirected: false));
        Assert.False(ColourSupport.Enabled(noColorOption: false, Empty, outputIsRedirected: true));
    }

    [Fact]
    public void An_empty_NO_COLOR_does_not_suppress_colour()
    {
        // no-color.org: present and *not an empty string*, whatever its value.
        string? EmptyNoColor(string name) => name == "NO_COLOR" ? "" : null;

        Assert.True(ColourSupport.Enabled(noColorOption: false, EmptyNoColor, outputIsRedirected: false));
    }

    // ---- Option parsing -------------------------------------------------------------------

    [Fact]
    public void Every_option_reaches_the_request()
    {
        SelectRequest? captured = null;
        var commandLine = new ReachCommandLine((request, _) =>
        {
            captured = request;
            return Task.FromResult(0);
        });

        var parseResult = commandLine.Parse(
        [
            "select", "My.slnx",
            "--base", "origin/main",
            "-c", "Release",
            "-o", "out",
            "--artifacts-path", "artifacts",
            "--no-build",
            "--report", "r.json",
            "--report-dir", "rdir",
            "--paths",
            "--list-unselected",
            "--runsettings", "my.runsettings",
            "-v", "diag",
            "--no-color",
        ]);

        Assert.Empty(parseResult.Errors);

        var request = commandLine.RequestFrom(parseResult);

        Assert.Equal("My.slnx", request.Target);
        Assert.Equal("origin/main", request.Base);
        Assert.Equal("Release", request.Configuration);
        Assert.Equal("out", request.Output);
        Assert.Equal("artifacts", request.ArtifactsPath);
        Assert.True(request.NoBuild);
        Assert.Equal("r.json", request.Report);
        Assert.Equal("rdir", request.ReportDirectory);
        Assert.True(request.Paths);
        Assert.True(request.ListUnselected);
        Assert.Equal("my.runsettings", request.RunSettings);
        Assert.Equal(Verbosity.Diagnostic, request.Verbosity);
        Assert.True(request.NoColor);
        Assert.Null(captured);
    }

    [Theory]
    [InlineData("q", nameof(Verbosity.Quiet))]
    [InlineData("quiet", nameof(Verbosity.Quiet))]
    [InlineData("m", nameof(Verbosity.Minimal))]
    [InlineData("minimal", nameof(Verbosity.Minimal))]
    [InlineData("n", nameof(Verbosity.Normal))]
    [InlineData("normal", nameof(Verbosity.Normal))]
    [InlineData("d", nameof(Verbosity.Detailed))]
    [InlineData("detailed", nameof(Verbosity.Detailed))]
    [InlineData("diag", nameof(Verbosity.Diagnostic))]
    [InlineData("diagnostic", nameof(Verbosity.Diagnostic))]
    public void Verbosity_mirrors_the_SDKs_spellings(string spelling, string expected) =>
        Assert.Equal(expected, Verbosities.Parse(spelling).ToString());

    // ---- Exit codes -----------------------------------------------------------------------

    [Fact]
    public async Task A_target_with_no_tests_is_exit_1()
    {
        using var tree = ProjectTree.Create();

        tree.AddProject("Worker", targetFrameworks: "net10.0");

        var exitCode = await Run(tree.Root, "select", "src/Worker/Worker.csproj");

        Assert.Equal((int)ExitCode.UsageError, exitCode);
        Assert.Contains("Worker.csproj", Out + Error);
    }

    [Fact]
    public async Task An_unresolvable_baseline_is_exit_4()
    {
        using var tree = SolutionWithTests();

        // No --base, no CI variables, and the temporary directory is no git repository at
        // all, so the shallow probe fails and resolution stops.
        var exitCode = await Run(tree.Root, "select");

        Assert.Equal((int)ExitCode.BaselineUnresolvable, exitCode);
    }

    [Fact]
    public async Task Exit_4_under_a_simulated_GitHub_Actions_environment_prints_the_line_to_add()
    {
        using var tree = SolutionWithTests();

        environment["GITHUB_ACTIONS"] = "true";

        Assert.Equal((int)ExitCode.BaselineUnresolvable, await Run(tree.Root, "select"));
        Assert.Contains("fetch-depth: 0", Out + Error);
    }

    [Fact]
    public async Task A_run_whose_phases_do_not_exist_yet_is_exit_70_and_says_so()
    {
        using var repository = TempRepository.Create();

        var tree = SolutionWithTests();

        try
        {
            // A real repository, so target discovery, analysis scope and baseline resolution
            // all succeed and the run stops at the first phase that is not built.
            CopyInto(tree.Root, repository.Path);
            repository.Commit("baseline");

            var exitCode = await Run(repository.Path, "select", "--base", "main");

            Assert.Equal((int)ExitCode.InternalError, exitCode);
            Assert.Contains("not implemented yet", Out + Error);

            // And the phases that do exist reported what they found.
            Assert.Contains("baseline-resolved", Error);
        }
        finally
        {
            tree.Dispose();
        }
    }

    private static void CopyInto(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static ProjectTree SolutionWithTests()
    {
        var tree = ProjectTree.Create();

        var core = tree.AddProject("Core", targetFrameworks: "net10.0");
        var tests = tree.AddProject("Tests", [core], [TestPackage], "net10.0");
        tree.AddSlnx("Solution", core, tests);

        return tree;
    }
}
