namespace Reach.Changes;

/// <summary>What git said happened to a path between the baseline and now.</summary>
internal enum ChangeStatus
{
    Added,
    Modified,
    Deleted,

    /// <summary>
    /// Never committed. Always included, with no flag to switch it off: opt-in under-selects
    /// by default, and the pathological case a <c>--no-untracked</c> would exist for belongs
    /// in the consumer's <c>.gitignore</c>.
    /// </summary>
    Untracked,
}

/// <summary>
/// One path from git, repository-relative and forward-slashed — the spelling git itself
/// returns, which is never re-interpreted by git afterwards.
/// </summary>
internal sealed record ChangedPath(string Path, ChangeStatus Status)
{
    internal bool IsCSharp =>
        Path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);

    /// <summary>The path existed at the baseline, so there is a revision to read.</summary>
    internal bool HasBaseline => Status is ChangeStatus.Modified or ChangeStatus.Deleted;

    /// <summary>The path exists in the working tree now.</summary>
    internal bool HasWorkingTree => Status is not ChangeStatus.Deleted;

    /// <summary>Parses one <c>git diff --name-status</c> line.</summary>
    internal static ChangedPath? FromNameStatus(string line)
    {
        var separator = line.IndexOf('\t');

        if (separator <= 0)
        {
            return null;
        }

        var path = line[(separator + 1)..].Trim();

        // Only the first letter matters: git writes M, A, D, T, U, X and the score-suffixed
        // R and C, which --no-renames means we never see.
        return line[0] switch
        {
            'A' => new ChangedPath(path, ChangeStatus.Added),
            'D' => new ChangedPath(path, ChangeStatus.Deleted),
            // T (type change), U (unmerged) and anything unrecognised are treated as a
            // modification, which is the reading that looks at both revisions.
            _ => new ChangedPath(path, ChangeStatus.Modified),
        };
    }
}
