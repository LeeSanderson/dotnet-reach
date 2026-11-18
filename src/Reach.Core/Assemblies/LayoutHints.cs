namespace Reach.Assemblies;

/// <summary>
/// <c>-c</c>, <c>-o</c> and <c>--artifacts-path</c> as discovery sees them: <strong>filters
/// applied to scan results, not layout inputs</strong>.
/// </summary>
/// <remarks>
/// Their only job is to break an ambiguity, which is why none of them is required for the
/// common case. Reach never computes a path from them — that is the whole point of scanning.
/// </remarks>
internal sealed record LayoutHints(string? Configuration, string? Output, string? ArtifactsPath)
{
    private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    internal static LayoutHints None { get; } = new(null, null, null);

    internal static LayoutHints From(SelectRequest request) =>
        new(
            request.Configuration,
            Absolute(request.Output, request.WorkingDirectory),
            Absolute(request.ArtifactsPath, request.WorkingDirectory));

    internal bool Keeps(ScannedAssembly assembly) =>
        UnderConfiguration(assembly.Path)
        && UnderIfGiven(assembly.Path, Output)
        && UnderIfGiven(assembly.Path, ArtifactsPath);

    /// <summary>
    /// What to tell someone whose scan came back ambiguous.
    /// </summary>
    internal string Suggestion() =>
        Configuration is null
            ? "Name the configuration with `-c <name>`, or the output directory with "
                + "`-o <dir>` or `--artifacts-path <dir>`, to say which of these to read."
            : $"`-c {Configuration}` did not narrow this to one. Name the output directory "
                + "with `-o <dir>` or `--artifacts-path <dir>` instead.";

    /// <summary>
    /// Matched as a whole path segment. Artifacts output lower-cases the configuration name,
    /// and the default layout does not, so the comparison ignores case on every platform
    /// rather than deferring to the filesystem's own rule.
    /// </summary>
    private bool UnderConfiguration(string path) =>
        Configuration is null
        || Array.Exists(
            path.Split(Separators),
            segment => segment.Equals(Configuration, StringComparison.OrdinalIgnoreCase));

    private static bool UnderIfGiven(string path, string? directory) =>
        directory is null || Paths.IsUnder(path, directory);

    private static string? Absolute(string? path, string workingDirectory) =>
        string.IsNullOrEmpty(path) ? null : Paths.Normalise(Path.GetFullPath(path, workingDirectory));
}
