using System.Diagnostics;

namespace Reach.Tests.Fixtures;

/// <summary>
/// A fresh git repository in a temporary directory, with a baseline commit of its own.
/// A fixture committed in <em>this</em> repository has this repository's history, which is
/// not a usable baseline — so every test that needs history builds one.
/// </summary>
internal sealed class TempRepository : IDisposable
{
    private readonly TempDirectory directory;

    private TempRepository(TempDirectory directory) => this.directory = directory;

    internal string Path => directory.Path;

    /// <summary>Creates an initialised repository with no commits yet.</summary>
    internal static TempRepository Create(string name = "reach-repo")
    {
        var directory = TempDirectory.Create(name);
        var repository = new TempRepository(directory);

        repository.Git("init", "--initial-branch=main");

        // Unset on hosted runners, where `git commit` then fails. Set per-repository so the
        // suite never depends on, or disturbs, the machine's own identity.
        repository.Git("config", "user.email", "tests@dotnet-reach.invalid");
        repository.Git("config", "user.name", "Reach Tests");
        repository.Git("config", "commit.gpgsign", "false");

        return repository;
    }

    internal string WriteFile(string relativePath, string contents) =>
        directory.WriteFile(relativePath, contents);

    internal string Combine(params string[] parts) => directory.Combine(parts);

    /// <summary>Stages everything and commits, returning the new commit's SHA.</summary>
    internal string Commit(string message)
    {
        Git("add", "--all");
        Git("commit", "--message", message, "--allow-empty");
        return Git("rev-parse", "HEAD").Trim();
    }

    internal void Checkout(string reference) => Git("checkout", reference);

    internal void CheckoutNewBranch(string name) => Git("checkout", "-b", name);

    /// <summary>Runs git and returns standard output, throwing on a non-zero exit.</summary>
    internal string Git(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = Path,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("git did not start.");

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git {string.Join(' ', arguments)} exited {process.ExitCode}: {standardError.Result}");
        }

        return standardOutput.Result;
    }

    public void Dispose() => directory.Dispose();
}
