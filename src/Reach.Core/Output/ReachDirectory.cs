namespace Reach.Output;

/// <summary>
/// The one directory Reach owns: <c>.reach/</c> under the <em>working</em> directory, not
/// beside the solution. Holds <c>report.json</c> and every response file and testlist.
/// </summary>
/// <remarks>
/// <strong>Reach never cleans it up.</strong> The runner reads those files in a <em>later</em>
/// pipeline step, so deleting them on exit would break the contract Reach just published. The
/// lifecycle is the caller's, and the documentation says so.
/// </remarks>
internal sealed class ReachDirectory
{
    internal const string DefaultName = ".reach";

    private const string GitIgnoreName = ".gitignore";

    private ReachDirectory(string path) => Path = path;

    internal string Path { get; }

    internal bool Exists => Directory.Exists(Path);

    /// <summary><c>--report-dir</c> moves the whole directory; the default sits under the working directory.</summary>
    internal static ReachDirectory For(string workingDirectory, string? reportDirectory) =>
        new(System.IO.Path.GetFullPath(
            reportDirectory is { Length: > 0 } ? reportDirectory : DefaultName,
            workingDirectory));

    internal string Combine(string fileName) => System.IO.Path.Combine(Path, fileName);

    /// <summary>
    /// Creates the directory and, on first use only, writes a <c>.gitignore</c> containing
    /// <c>*</c> so the first adopter to run Reach locally does not commit a report. An
    /// existing one is left alone: it may have been edited deliberately.
    /// </summary>
    internal void EnsureCreated()
    {
        Directory.CreateDirectory(Path);

        var gitignore = Combine(GitIgnoreName);

        if (!File.Exists(gitignore))
        {
            File.WriteAllText(gitignore, "*" + Environment.NewLine);
        }
    }
}
