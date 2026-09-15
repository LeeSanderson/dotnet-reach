using Reach.Git;

namespace Reach.Assemblies;

/// <summary>
/// Which source files git can see, and therefore which ones change detection can ever report
/// a change to.
/// </summary>
/// <remarks>
/// <c>.gitignore</c> governs only <em>untracked</em> files, so the blind spot is precisely
/// untracked-<strong>and</strong>-ignored compiled source: a file that is neither in the index
/// nor listed as an ordinary untracked file. In CI most of that population is derived rather
/// than authored; what remains genuinely invisible is a pipeline step generating code from a
/// source outside the repository.
/// </remarks>
internal sealed class SourceVisibility
{
    private readonly string repositoryRoot;
    private readonly HashSet<string> visible;

    private SourceVisibility(string repositoryRoot, HashSet<string> visible)
    {
        this.repositoryRoot = repositoryRoot;
        this.visible = visible;
    }

    /// <summary>Nothing is known about visibility, so nothing is reported as invisible.</summary>
    internal static SourceVisibility Unknown { get; } = new(string.Empty, []);

    internal static async Task<SourceVisibility> ReadAsync(
        GitAdapter git,
        string repositoryRoot,
        CancellationToken cancellationToken = default)
    {
        var tracked = await git.TrackedPathsAsync(cancellationToken).ConfigureAwait(false);
        var untracked = await git.UntrackedPathsAsync(cancellationToken).ConfigureAwait(false);

        if (!tracked.Succeeded)
        {
            return Unknown;
        }

        var visible = new HashSet<string>(Paths.Comparer);

        foreach (var path in tracked.Lines.Concat(untracked.Lines))
        {
            visible.Add(Paths.Normalise(Path.Combine(repositoryRoot, path)));
        }

        return new SourceVisibility(Paths.Normalise(repositoryRoot), visible);
    }

    /// <summary>
    /// On disk, under the repository, and neither tracked nor reported as an ordinary
    /// untracked file — which leaves exactly one possibility.
    /// </summary>
    internal bool IsInvisible(string path)
    {
        if (repositoryRoot.Length == 0)
        {
            return false;
        }

        var normalised = Paths.Normalise(path);

        return Paths.IsUnder(normalised, repositoryRoot) && !visible.Contains(normalised);
    }
}
