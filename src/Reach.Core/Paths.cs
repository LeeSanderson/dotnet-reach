namespace Reach;

/// <summary>
/// One place that decides how two paths are compared. Separators and case sensitivity are
/// exactly where Windows and Linux differ, Reach routes a changed file by matching debug-symbol
/// document paths against the working tree, and a path-comparison bug is silent under-selection
/// rather than a crash — so this is not a convenience.
/// </summary>
internal static class Paths
{
    /// <summary>Matches the filesystem: case-insensitive on Windows and macOS, case-sensitive elsewhere.</summary>
    internal static StringComparer Comparer { get; } =
        CaseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static StringComparison Comparison { get; } =
        CaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool CaseInsensitive { get; } =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <summary>
    /// Rooted, with this platform's separators and no trailing one. Everything Reach compares
    /// goes through here first, so the comparison is between two spellings of the same shape.
    /// </summary>
    internal static string Normalise(string path)
    {
        var full = Path.GetFullPath(path.Replace('\\', Path.DirectorySeparatorChar));

        return full.Length > 1 ? full.TrimEnd(Path.DirectorySeparatorChar) : full;
    }

    internal static bool Same(string left, string right) => Comparer.Equals(left, right);

    /// <summary>
    /// Whether <paramref name="path"/> sits at or under <paramref name="directory"/>. Used by
    /// the rule table's directory containment, where a prefix test alone would let
    /// <c>/src/Foo</c> claim <c>/src/FooBar</c>.
    /// </summary>
    internal static bool IsUnder(string path, string directory)
    {
        var normalisedDirectory = Normalise(directory);
        var normalisedPath = Normalise(path);

        if (Same(normalisedPath, normalisedDirectory))
        {
            return true;
        }

        return normalisedPath.StartsWith(normalisedDirectory, Comparison)
            && normalisedPath[normalisedDirectory.Length] == Path.DirectorySeparatorChar;
    }
}
