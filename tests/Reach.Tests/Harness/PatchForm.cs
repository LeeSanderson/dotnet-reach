using Reach.Tests.Acceptance;

namespace Reach.Tests.Harness;

/// <summary>
/// Whether a patch is in the checked-in stripped form (spec §3.1): no <c>diff --git</c>,
/// <c>index</c>, file-mode or other git extended header lines.
/// </summary>
/// <remarks>
/// Every line inside a hunk starts with a space, <c>+</c>, <c>-</c> or <c>\</c>, so a line that
/// starts with one of these headers can only be a header.
/// </remarks>
internal static class PatchForm
{
    private static readonly string[] ExtendedHeaders =
    [
        "diff --git ",
        "index ",
        "old mode ",
        "new mode ",
        "new file mode ",
        "deleted file mode ",
        "similarity index ",
        "dissimilarity index ",
        "rename from ",
        "rename to ",
        "copy from ",
        "copy to ",
    ];

    /// <summary>
    /// One failure for a patch that is not in the stripped form, listing every offending line and
    /// the commands that rewrite it; none for a patch that is.
    /// </summary>
    /// <param name="relativePath">The patch's path under <c>tests/Reach.Tests/Acceptance/</c>, which
    /// starts with its sample's folder.</param>
    /// <param name="text">The patch's contents.</param>
    internal static IReadOnlyList<string> Failures(string relativePath, string text)
    {
        var offending = text
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => Array.Exists(ExtendedHeaders, header => entry.Line.StartsWith(header, StringComparison.Ordinal)))
            .Select(entry => $"Line {entry.Number}: {entry.Line}")
            .ToArray();

        if (offending.Length == 0)
        {
            return [];
        }

        var segments = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        var sample = segments[0];
        var name = Path.GetFileNameWithoutExtension(segments[^1]);

        return
        [
            $"{relativePath} is not in the stripped form. {string.Join("\n  ", offending)}\n"
            + "  Only ---/+++ file headers and @@ hunks belong in a checked-in patch. To fix, apply it and save it again:\n"
            + $"  {PatchTool.Apply(sample, name)}\n"
            + $"  {PatchTool.Save(sample, name)}\n"
            + "  (add --on <baseline> to both if it was written on a baseline patch)",
        ];
    }
}
