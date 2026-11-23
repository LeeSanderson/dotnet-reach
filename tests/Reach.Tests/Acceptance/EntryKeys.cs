using Reach.Reporting;

namespace Reach.Tests.Acceptance;

/// <summary>
/// How an acceptance test names a report entry: the <c>.csproj</c> file name without its
/// extension, or <c>Name@tfm</c> for one framework of a multi-targeted project.
/// </summary>
internal sealed class EntryKeys(IReadOnlyList<ReportEntry> entries)
{
    internal IReadOnlyList<ReportEntry> Entries { get; } = entries;

    /// <summary>
    /// The key a failure message names an entry by: the bare name when it is the only entry
    /// with that name, otherwise <c>Name@tfm</c>.
    /// </summary>
    internal string KeyOf(ReportEntry entry) =>
        Entries.Count(other => NameOf(other) == NameOf(entry)) == 1
            ? NameOf(entry)
            : Qualified(entry);

    /// <summary>
    /// The one entry <paramref name="key"/> names, or null after adding to
    /// <paramref name="differences"/> why there is not exactly one.
    /// </summary>
    /// <remarks>
    /// A bare name that matches several entries is an error rather than a match for all of
    /// them, so a framework cannot be skipped by accident.
    /// </remarks>
    internal ReportEntry? Resolve(string key, List<string> differences)
    {
        var matches = key.Contains('@', StringComparison.Ordinal)
            ? Entries.Where(entry => Qualified(entry) == key).ToArray()
            : Entries.Where(entry => NameOf(entry) == key).ToArray();

        switch (matches.Length)
        {
            case 1:
                return matches[0];
            case 0:
                differences.Add($"{key}: no test project in the report has this key");
                return null;
            default:
                var qualified = string.Join(", ", matches.Select(Qualified).Order(StringComparer.Ordinal));
                differences.Add(
                    $"{key}: matches {matches.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)} entries ({qualified}); write Name@tfm");
                return null;
        }
    }

    private static string NameOf(ReportEntry entry) => Path.GetFileNameWithoutExtension(entry.Project);

    private static string Qualified(ReportEntry entry) => $"{NameOf(entry)}@{entry.TargetFramework}";
}
