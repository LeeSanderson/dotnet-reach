namespace Reach.Tests.Fixtures;

/// <summary>
/// The repository this suite lives in.
/// </summary>
/// <remarks>
/// Several tests read files the repository ships rather than files they wrote — the limitations
/// register, the committed example report, the dogfood workflow — and none of them can rely on
/// the working directory, which the test host chooses. Nor can they rely on the compile-time
/// source path: a continuous-integration build is deterministic, so <c>[CallerFilePath]</c>
/// becomes <c>/_/tests/...</c>, which exists on no machine. The test assembly itself is built
/// under <c>tests/Reach.Tests/bin</c>, inside the repository, so the search starts there.
/// </remarks>
internal static class RepositoryRoot
{
    /// <summary>
    /// The nearest directory above the test assembly that holds a <c>.git</c>.
    /// </summary>
    /// <remarks>
    /// <c>.git</c> is a directory in a clone but a file in a linked worktree. Looking only for
    /// the directory would skip a worktree's own root and land on the main checkout above it.
    /// </remarks>
    internal static string Of()
    {
        var start = AppContext.BaseDirectory;
        var directory = start;

        while (directory is not null && !Path.Exists(Path.Combine(directory, ".git")))
        {
            directory = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(directory));
        }

        return directory
            ?? throw new InvalidOperationException($"No repository above {start}.");
    }

    /// <summary>
    /// A path inside the repository, from its root.
    /// </summary>
    internal static string Combine(params string[] segments) =>
        Path.Combine([Of(), .. segments]);

    /// <summary>
    /// The text of a file the repository ships, failing the test when it is missing.
    /// </summary>
    /// <remarks>
    /// Line endings are normalised, and only line endings. Git hands a Windows checkout CRLF and
    /// a Linux one LF, and a test that failed on the platform rather than on the content would
    /// be reporting the wrong thing.
    /// </remarks>
    internal static string ReadText(params string[] segments)
    {
        var path = Combine(segments);

        Assert.True(File.Exists(path), $"Nothing at {path}.");

        return File.ReadAllText(path).ReplaceLineEndings("\n");
    }
}
