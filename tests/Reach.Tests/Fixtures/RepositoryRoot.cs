using System.Runtime.CompilerServices;

namespace Reach.Tests.Fixtures;

/// <summary>
/// The repository this suite lives in.
/// </summary>
/// <remarks>
/// Several tests read files the repository ships rather than files they wrote — the limitations
/// register, the committed example report, the dogfood workflow — and none of them can rely on
/// the working directory, which the test host chooses. <see cref="CallerFilePathAttribute"/> is
/// filled in by the compiler, so it survives being run from anywhere.
/// </remarks>
internal static class RepositoryRoot
{
    /// <summary>
    /// The nearest directory above <paramref name="callerFile"/> that holds a <c>.git</c>.
    /// </summary>
    /// <param name="callerFile">The source file to start from; the compiler fills it in.</param>
    /// <remarks>
    /// <c>.git</c> is a directory in a clone but a file in a linked worktree. Looking only for
    /// the directory would skip a worktree's own root and land on the main checkout above it.
    /// </remarks>
    internal static string Of([CallerFilePath] string callerFile = "")
    {
        var directory = Path.GetDirectoryName(callerFile);

        while (directory is not null && !Path.Exists(Path.Combine(directory, ".git")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory
            ?? throw new InvalidOperationException($"No repository above {callerFile}.");
    }

    /// <summary>
    /// A path inside the repository, from its root.
    /// </summary>
    internal static string Combine(params string[] segments) =>
        Path.Combine([Of(), .. segments]);
}
