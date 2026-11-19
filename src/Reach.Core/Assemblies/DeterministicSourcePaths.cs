namespace Reach.Assemblies;

/// <summary>
/// The SDK's deterministic source paths (<c>DeterministicSourcePaths</c>, on by default when
/// <c>ContinuousIntegrationBuild</c> is), read back. See ADR-0018.
/// </summary>
/// <remarks>
/// <para>
/// <c>MapSourceRoots</c> gives each top-level <c>SourceRoot</c> the prefix
/// <c>"/_" + (index == 0 ? "" : index) + "/"</c>, numbering source-controlled roots first, and
/// gives a nested root (a git submodule) its containing root's prefix plus its relative path.
/// The compiler's <c>-pathmap</c> then rewrites every recorded path. Neither the SourceLink map
/// (prefix to URL) nor the recorded compilation options (which leave <c>pathmap</c> out)
/// record which local directory a prefix stood for.
/// </para>
/// <para>
/// So <c>/_/</c> is the git top-level, submodules included. The further roots are the ones
/// restore adds — every NuGet package folder, which is where package content files compiled
/// into a project come from — and any a build adds itself. Their order is not something to
/// rely on, so a document under one is mapped only to a file that exists in the global
/// packages folder at the same relative path, and correspondence then checks its checksum.
/// Anything else is left unmapped, never guessed.
/// </para>
/// </remarks>
internal static class DeterministicSourcePaths
{
    private const string RepositoryPrefix = "/_/";

    /// <summary>
    /// What every deterministic prefix starts with, before its optional number.
    /// </summary>
    private const string RootStart = "/_";

    internal static DocumentPath Map(string recorded, SourceRoots roots)
    {
        if (recorded.StartsWith(RepositoryPrefix, StringComparison.Ordinal) && roots.Repository.Length > 0)
        {
            return new DocumentPath(
                Paths.Normalise(Path.Combine(roots.Repository, recorded[RepositoryPrefix.Length..])),
                UnmappedRoot: null,
                ReadAsRepositoryRoot: true);
        }

        if (RootOf(recorded) is not { } root)
        {
            return new DocumentPath(Paths.Normalise(recorded), UnmappedRoot: null);
        }

        if (roots.Packages is not null
            && root != RepositoryPrefix
            && Path.Combine(roots.Packages, recorded[root.Length..]) is var package
            && File.Exists(package))
        {
            return new DocumentPath(Paths.Normalise(package), UnmappedRoot: null);
        }

        return new DocumentPath(Paths.Normalise(recorded), root);
    }

    /// <summary>
    /// The <c>/_/</c> or <c>/_N/</c> prefix the path was recorded under, or null.
    /// </summary>
    private static string? RootOf(string recorded)
    {
        if (!recorded.StartsWith(RootStart, StringComparison.Ordinal))
        {
            return null;
        }

        var end = recorded.IndexOf('/', RootStart.Length);

        if (end < 0)
        {
            return null;
        }

        foreach (var character in recorded.AsSpan(RootStart.Length, end - RootStart.Length))
        {
            if (!char.IsAsciiDigit(character))
            {
                return null;
            }
        }

        return recorded[..(end + 1)];
    }
}

/// <summary>
/// The directories a deterministic build's path prefixes can stand for on this machine.
/// </summary>
/// <param name="Repository">
/// The git top-level of the working tree, which the SDK records as <c>/_/</c>. Empty when git
/// could not report it, and then <c>/_/</c> is not mapped.
/// </param>
/// <param name="Packages">
/// The NuGet global packages folder, where the files under a further root are looked for; null
/// to look nowhere.
/// </param>
internal sealed record SourceRoots(string Repository, string? Packages)
{
    /// <summary>
    /// The working tree, and the global packages folder NuGet uses unless it is configured
    /// elsewhere: <c>NUGET_PACKAGES</c>, else <c>~/.nuget/packages</c>. A folder configured
    /// another way is not found, and its files then read as an unmapped root.
    /// </summary>
    internal static SourceRoots Of(string repositoryRoot) => new(repositoryRoot, GlobalPackagesFolder());

    private static string? GlobalPackagesFolder()
    {
        var configured = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

        if (!string.IsNullOrEmpty(configured))
        {
            return configured;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return home.Length == 0 ? null : Path.Combine(home, ".nuget", "packages");
    }
}

/// <summary>
/// Where a recorded document sits on this machine.
/// </summary>
/// <param name="Path">Absolute and normalised to this machine's separators.</param>
/// <param name="UnmappedRoot">The deterministic root left unmapped, or null.</param>
/// <param name="ReadAsRepositoryRoot">
/// Recorded under <c>/_/</c> and read as the repository root: the convention a wrong
/// <c>SourceRoot</c> breaks, so the one correspondence checks.
/// </param>
internal readonly record struct DocumentPath(string Path, string? UnmappedRoot, bool ReadAsRepositoryRoot = false);
