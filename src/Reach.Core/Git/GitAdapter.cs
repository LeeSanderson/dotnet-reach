using Reach.Processes;

namespace Reach.Git;

/// <summary>
/// The git operations every later phase needs, each an argument vector through
/// <see cref="IProcessRunner"/> and never a shell string.
/// </summary>
/// <remarks>
/// Deliberately thin and deliberately honest: it returns the child's exit code and streams
/// rather than interpreting them. Every caller has something different to say about a
/// failure — a missing merge-base is exit 4, a shallow clone is a notice — so the adapter
/// decides nothing.
/// </remarks>
internal sealed class GitAdapter(IProcessRunner runner, string workingDirectory)
{
    /// <summary>
    /// Applied to every call. Without it git returns a path containing a non-ASCII character
    /// as <c>"caf\303\251.cs"</c>, quoted and octal-escaped, which then matches no file in
    /// the working tree — silent under-selection rather than an error.
    /// </summary>
    private static readonly string[] GlobalOptions = ["-c", "core.quotePath=false"];

    /// <summary>The commit a change is measured against, as the merge-base with a reference.</summary>
    internal Task<ProcessResult> MergeBaseAsync(string reference, CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "merge-base", "HEAD", reference);

    /// <summary>Resolves a reference to a SHA. Also how a reference is shown to exist at all.</summary>
    internal Task<ProcessResult> RevParseAsync(string reference, CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "rev-parse", reference);

    /// <summary>Reports <c>true</c> or <c>false</c> on standard output. A shallow clone has no merge-base to find.</summary>
    internal Task<ProcessResult> IsShallowCloneAsync(CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "rev-parse", "--is-shallow-repository");

    /// <summary>
    /// Committed-since-baseline, staged and unstaged in one shot, as <c>&lt;status&gt;\t&lt;path&gt;</c> lines.
    /// </summary>
    /// <remarks>
    /// <c>--no-renames</c> is load-bearing, not tidiness: it is what makes the changed set's
    /// declared-type matching the ground truth rather than git's similarity threshold, whose
    /// wrong setting under-selects.
    /// </remarks>
    internal Task<ProcessResult> ChangedPathsAsync(string baseline, CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "diff", "--no-renames", "--name-status", baseline);

    /// <summary>
    /// Untracked files, which are always included with no flag to switch them off.
    /// </summary>
    /// <remarks>
    /// <c>--exclude-standard</c> is load-bearing too: without it every generated <c>.cs</c>
    /// under <c>obj/</c> returns as untracked and every project's assembly widens on every run.
    /// </remarks>
    internal Task<ProcessResult> UntrackedPathsAsync(CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "ls-files", "--others", "--exclude-standard");

    /// <summary>
    /// A file's contents at the baseline. Exits non-zero when the path did not exist there,
    /// which is how an added file is told from a modified one without a second command.
    /// </summary>
    /// <param name="path">Repository-relative, forward-slashed — the spelling git itself returns.</param>
    internal Task<ProcessResult> FileAtBaselineAsync(
        string baseline,
        string path,
        CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "show", $"{baseline}:{path}");

    /// <summary>
    /// Every path in the index. With <see cref="UntrackedPathsAsync"/> it spans everything git
    /// can see, which is what makes the complement — untracked <em>and</em> ignored — nameable.
    /// </summary>
    internal Task<ProcessResult> TrackedPathsAsync(CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "ls-files", "--cached");

    /// <summary>The working tree's root, which is what repository-relative paths are relative to.</summary>
    internal Task<ProcessResult> RepositoryRootAsync(CancellationToken cancellationToken = default) =>
        RunAsync(cancellationToken, "rev-parse", "--show-toplevel");

    private Task<ProcessResult> RunAsync(CancellationToken cancellationToken, params string[] arguments) =>
        runner.RunAsync(
            new ProcessRequest("git", [.. GlobalOptions, .. arguments], workingDirectory),
            cancellationToken);
}
