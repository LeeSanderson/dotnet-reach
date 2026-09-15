using System.Runtime.CompilerServices;
using Reach.Reporting;

namespace Reach.Tests.Fixtures;

/// <summary>
/// The fixture solution, copied into a fresh temporary git repository and built once.
/// </summary>
/// <remarks>
/// <para>
/// A fixture committed in <em>this</em> repository has this repository's history, which is not a
/// usable baseline; and committing fixture <em>history</em> would make every test depend on a
/// real commit graph nobody can read. So the copy is the subject, and the baseline is a commit
/// the test made.
/// </para>
/// <para>
/// Built once per test class through xUnit's class fixture, because a <c>dotnet build</c> is
/// seconds at best — which is exactly why everything that can be tested in memory is.
/// </para>
/// </remarks>
internal sealed class FixtureSolution : IDisposable
{
    private readonly TempRepository repository;

    private FixtureSolution(TempRepository repository) => this.repository = repository;

    internal string Path => repository.Path;

    internal string Solution => repository.Combine("Contoso.slnx");

    /// <summary>Copies, commits the baseline, and builds. The returned SHA is the baseline.</summary>
    internal static FixtureSolution Build(out string baseline, [CallerFilePath] string testFile = "")
    {
        var repository = TempRepository.Create("reach-contoso");
        var fixture = new FixtureSolution(repository);

        Copy(SourceDirectory(testFile), repository.Path);

        repository.WriteFile(".gitignore", "bin/\nobj/\n");
        baseline = repository.Commit("baseline");

        var build = Run("dotnet", ["build", fixture.Solution], repository.Path);

        Assert.True(build.ExitCode == 0, "the fixture solution did not build:\n" + build.Output);

        return fixture;
    }

    internal string WriteFile(string relativePath, string contents) =>
        repository.WriteFile(relativePath, contents);

    internal string ReadFile(string relativePath) =>
        File.ReadAllText(System.IO.Path.Combine(Path, relativePath));

    internal void Delete(string relativePath) =>
        File.Delete(System.IO.Path.Combine(Path, relativePath));

    /// <summary>Rebuilds after a change, for the assertions that need the binaries to agree.</summary>
    internal void Rebuild()
    {
        var build = Run("dotnet", ["build", Solution], Path);

        Assert.True(build.ExitCode == 0, "the fixture solution did not rebuild:\n" + build.Output);
    }

    /// <summary>Runs Reach over the fixture and returns the report it wrote.</summary>
    internal async Task<Report> SelectAsync(string baseline, CancellationToken cancellationToken, bool paths = false)
    {
        var timings = new PhaseTimings();

        var run = await new ReachPipeline(new Reach.Processes.ProcessRunner())
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

    private static string SourceDirectory(string testFile)
    {
        var directory = System.IO.Path.GetDirectoryName(testFile);

        while (directory is not null && !Directory.Exists(System.IO.Path.Combine(directory, ".git")))
        {
            directory = System.IO.Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);

        var fixture = System.IO.Path.Combine(directory, "tests", "fixtures", "Contoso");

        Assert.True(Directory.Exists(fixture), $"No fixture solution at {fixture}.");

        return fixture;
    }

    private static void Copy(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = System.IO.Path.Combine(destination, System.IO.Path.GetRelativePath(source, file));

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static (int ExitCode, string Output) Run(string executable, string[] arguments, string workingDirectory)
    {
        var result = new Reach.Processes.ProcessRunner()
            .RunAsync(new Reach.Processes.ProcessRequest(executable, arguments, workingDirectory))
            .GetAwaiter()
            .GetResult();

        return (result.ExitCode, result.StandardOutput + result.StandardError);
    }

    public void Dispose() => repository.Dispose();
}
