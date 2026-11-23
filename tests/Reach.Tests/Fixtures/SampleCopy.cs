using Reach.Processes;
using Reach.Reporting;
using Reach.Tests.Acceptance;

namespace Reach.Tests.Fixtures;

/// <summary>
/// A sample solution copied into a fresh temporary git repository, patched, built, and run
/// through Reach: the harness behind every acceptance test.
/// </summary>
/// <remarks>
/// <para>
/// A sample committed in <em>this</em> repository has this repository's history, which is not a
/// usable baseline; and committing sample <em>history</em> would make every test depend on a
/// real commit graph nobody can read. So the copy is the subject, and the baseline is a commit
/// the test made.
/// </para>
/// <para>
/// Each test gets its own temp root, deleted on dispose, holding two folders side by side:
/// <c>repo/</c>, the copied sample and the patch result and nothing else, because Reach treats
/// every untracked file under its root as a change; and <c>out/</c>, for everything the harness
/// and Reach write.
/// </para>
/// </remarks>
internal sealed class SampleCopy : IDisposable
{
    private const string FixLine = "dotnet run tests/tools/patch.cs --";

    private const int BuildTailLines = 40;

    private readonly TempDirectory _root;
    private readonly TempRepository _repository;
    private readonly string _solution;
    private readonly string _sample;
    private readonly string _patches;
    private readonly string? _baselinePatch;
    private readonly HashSet<string> _touched = new(StringComparer.Ordinal);

    private SampleCopy(TempDirectory root, string sample, string solution, string patches, string? baselinePatch)
    {
        _root = root;
        _repository = TempRepository.CreateIn(root, "repo");
        _sample = sample;
        _solution = solution;
        _patches = patches;
        _baselinePatch = baselinePatch;

        Directory.CreateDirectory(Out);
    }

    internal string Path => _repository.Path;

    internal string Out => _root.Combine("out");

    internal string Solution => _repository.Combine(_solution);

    /// <summary>
    /// The commit every change is measured from.
    /// </summary>
    internal string Baseline { get; private set; } = string.Empty;

    /// <summary>
    /// Where a sample's patches live in the source tree. They are read from there, never copied
    /// to the build output.
    /// </summary>
    internal static string PatchFolderOf(string sample) =>
        RepositoryRoot.Combine("tests", "Reach.Tests", "Acceptance", sample);

    /// <summary>
    /// Copies <c>tests/samples/&lt;sample&gt;/</c>, applies the baseline patch when there is one,
    /// and commits the result as the baseline. Steps 1 to 3 of the harness.
    /// </summary>
    internal static SampleCopy Create(string sample, string patchFolder, string? baselinePatch = null)
    {
        var copy = new SampleCopy(
            TempDirectory.Create("reach-" + sample),
            sample,
            sample + ".slnx",
            patchFolder,
            baselinePatch);

        // The sample is checked out with CRLF on every platform, and so is every patch. Turning
        // conversion off makes git apply compare those bytes as they are, on Windows and Linux
        // alike, whatever the machine's own setting.
        copy._repository.Git("config", "core.autocrlf", "false");

        Copy(SampleDirectory(sample), copy.Path);

        if (baselinePatch is not null)
        {
            copy.Apply(baselinePatch, $"baseline: {baselinePatch}", on: null);
        }

        copy.CommitBaseline();

        return copy;
    }

    /// <summary>
    /// Applies each change patch in order, left uncommitted. Step 4.
    /// </summary>
    internal void ApplyChanges(IReadOnlyList<string> patches)
    {
        for (var index = 0; index < patches.Count; index++)
        {
            var patch = patches[index];

            Apply(patch, $"{index + 1} of {patches.Count}: {patch}", on: _baselinePatch);

            _touched.UnionWith(PathsTouchedBy(System.IO.Path.Combine(_patches, patch)));
        }
    }

    /// <summary>
    /// <c>git status</c> lists only the paths the change patches touched. Step 5, which must run
    /// before Reach does: a stray file in <c>repo/</c> would be a change Reach reports.
    /// </summary>
    internal void AssertOnlyPatchedPathsChanged()
    {
        var status = _repository.Git("status", "--porcelain=v1", "-z", "--untracked-files=all");

        var stray = status
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry[3..])
            .Where(path => !_touched.Contains(path))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stray.Length == 0,
            "git status lists paths no change patch touched, so Reach would see them as changes:\n  "
            + string.Join("\n  ", stray));
    }

    /// <summary>
    /// Builds the sample, then runs Reach twice. Steps 6 and 7.
    /// </summary>
    /// <remarks>
    /// When the build succeeds Reach runs with no build, both times: one build per test. When it
    /// fails the test does not fail here. Reach runs in its default build mode instead, so it
    /// invokes the build, sees it fail and reports the failure itself. That is how a row whose
    /// patch breaks the build proves its exit code end to end.
    /// </remarks>
    internal async Task<Observation> RunTwiceAsync(CancellationToken cancellationToken)
    {
        var build = Run("dotnet", ["build", Solution], Path);

        await File.WriteAllTextAsync(System.IO.Path.Combine(Out, "build.log"), build.Output, cancellationToken);

        var request = new SelectRequest
        {
            WorkingDirectory = Path,
            Base = Baseline,
            NoBuild = build.ExitCode == 0,
            ReportDirectory = Out,
        };

        var first = await RunReachAsync(request, 1, cancellationToken);
        var second = await RunReachAsync(request, 2, cancellationToken);

        return new Observation(first, second, _root.Path, build.ExitCode == 0 ? null : Tail(build.Output));
    }

    // ---- The Contoso API, until ticket 12 moves the last test off it ---------------------------

    /// <summary>
    /// Copies <c>tests/fixtures/Contoso/</c>, commits the baseline, and builds. The returned SHA
    /// is the baseline.
    /// </summary>
    internal static SampleCopy Build(out string baseline)
    {
        var copy = new SampleCopy(
            TempDirectory.Create("reach-contoso"),
            "Contoso",
            "Contoso.slnx",
            patches: string.Empty,
            baselinePatch: null);

        Copy(ContosoDirectory(), copy.Path);
        copy.CommitBaseline();
        baseline = copy.Baseline;

        var build = Run("dotnet", ["build", copy.Solution], copy.Path);

        Assert.True(build.ExitCode == 0, "the fixture solution did not build:\n" + build.Output);

        return copy;
    }

    internal string WriteFile(string relativePath, string contents) =>
        _repository.WriteFile(relativePath, contents);

    internal string ReadFile(string relativePath) =>
        File.ReadAllText(System.IO.Path.Combine(Path, relativePath));

    /// <summary>
    /// Rebuilds after a change, for the assertions that need the binaries to agree.
    /// </summary>
    internal void Rebuild()
    {
        var build = Run("dotnet", ["build", Solution], Path);

        Assert.True(build.ExitCode == 0, "the fixture solution did not rebuild:\n" + build.Output);
    }

    /// <summary>
    /// Runs Reach over the copy and returns the report it wrote.
    /// </summary>
    internal async Task<Report> SelectAsync(string baseline, CancellationToken cancellationToken, bool paths = false)
    {
        var timings = new PhaseTimings();

        var run = await new ReachPipeline(new ProcessRunner())
            .SelectAsync(
                new SelectRequest
                {
                    WorkingDirectory = Path,
                    Base = baseline,
                    NoBuild = true,
                    Paths = paths,
                },
                name => null,
                timings,
                cancellationToken);

        return ReportBuilder.Build(
            run,
            new SelectRequest { WorkingDirectory = Path, NoBuild = true },
            timings);
    }

    public void Dispose() => _repository.Dispose();

    // ---- The steps ------------------------------------------------------------------------------

    private void CommitBaseline()
    {
        _repository.WriteFile(".gitignore", "bin/\nobj/\n");
        Baseline = _repository.Commit("baseline");
    }

    /// <summary>
    /// <c>git apply --verbose</c>, never <c>--3way</c>: <c>git apply --3way --check</c> exits 0 on
    /// a patch that conflicts, which would hide drift.
    /// </summary>
    private void Apply(string patch, string role, string? on)
    {
        var path = System.IO.Path.Combine(_patches, patch);

        if (!File.Exists(path))
        {
            Assert.Fail($"{role}: no such patch at {path}.\n\n{Fix(patch, on)}");
        }

        var applied = Run("git", ["apply", "--verbose", path], Path);

        if (applied.ExitCode != 0)
        {
            Assert.Fail($"{role} did not apply. git apply --verbose said:\n{applied.Output.TrimEnd()}\n\n{Fix(patch, on)}");
        }
    }

    /// <summary>
    /// The routine for drift: apply the patch to the sample in the working tree, resolve, save.
    /// </summary>
    private string Fix(string patch, string? on)
    {
        var name = WithoutExtension(patch);
        var onBaseline = on is null ? string.Empty : $" --on {WithoutExtension(on)}";

        return $"To fix: {FixLine} apply {_sample} {name}{onBaseline}, resolve, "
            + $"then {FixLine} save {_sample} {name}{onBaseline} --force.";
    }

    private async Task<ReachRun> RunReachAsync(SelectRequest request, int number, CancellationToken cancellationToken)
    {
        var timings = new PhaseTimings();

        var run = await new ReachPipeline(new ProcessRunner())
            .SelectAsync(request, name => null, timings, cancellationToken);

        var report = ReportBuilder.Build(run, request, timings);

        await File.WriteAllTextAsync(
            System.IO.Path.Combine(Out, $"report-{number}.json"),
            ReportWriter.Serialise(report),
            cancellationToken);

        return new ReachRun(Convert.ToInt32(run.ExitCode, System.Globalization.CultureInfo.InvariantCulture), report);
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private static string WithoutExtension(string patch) =>
        patch.EndsWith(".patch", StringComparison.Ordinal) ? patch[..^".patch".Length] : patch;

    /// <summary>
    /// The paths a patch's file headers name, relative to the sample's root.
    /// </summary>
    private static IEnumerable<string> PathsTouchedBy(string patch) =>
        File.ReadLines(patch)
            .Where(line => line.StartsWith("--- ", StringComparison.Ordinal) || line.StartsWith("+++ ", StringComparison.Ordinal))
            .Select(line => line[4..].Split('\t')[0].TrimEnd('\r'))
            .Where(path => path != "/dev/null")
            .Select(path => path.StartsWith("a/", StringComparison.Ordinal) || path.StartsWith("b/", StringComparison.Ordinal) ? path[2..] : path);

    private static string Tail(string output) =>
        string.Join('\n', output.ReplaceLineEndings("\n").TrimEnd().Split('\n').TakeLast(BuildTailLines));

    private static string SampleDirectory(string sample)
    {
        var directory = RepositoryRoot.Combine("tests", "samples", sample);

        Assert.True(Directory.Exists(directory), $"No sample solution at {directory}.");

        return directory;
    }

    private static string ContosoDirectory()
    {
        var fixture = RepositoryRoot.Combine("tests", "fixtures", "Contoso");

        Assert.True(Directory.Exists(fixture), $"No fixture solution at {fixture}.");

        return fixture;
    }

    /// <summary>
    /// Copies the files git tracks under <paramref name="source"/>, and nothing else on disk.
    /// </summary>
    /// <remarks>
    /// The sample is built in place (CI does, to hold it to the coding standard), and that build
    /// output must not reach the temporary repository: Reach running with no build would find
    /// the extra assemblies.
    /// </remarks>
    private static void Copy(string source, string destination)
    {
        var listing = new ProcessRunner()
            .RunAsync(new ProcessRequest("git", ["ls-files", "-z"], source), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        Assert.True(listing.ExitCode == 0, "git ls-files failed over the sample:\n" + listing.StandardError);

        var files = listing.StandardOutput.Split('\0', StringSplitOptions.RemoveEmptyEntries);

        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var target = System.IO.Path.Combine(destination, file);

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(System.IO.Path.Combine(source, file), target, overwrite: true);
        }
    }

    private static (int ExitCode, string Output) Run(string executable, string[] arguments, string workingDirectory)
    {
        var result = new ProcessRunner()
            .RunAsync(new ProcessRequest(executable, arguments, workingDirectory), CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return (result.ExitCode, result.StandardOutput + result.StandardError);
    }
}
