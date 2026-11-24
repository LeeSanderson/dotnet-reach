using System.Text.Json;
using System.Text.RegularExpressions;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// The notice-code index in <c>docs/report-schema.md</c> lists exactly the codes
/// <see cref="NoticeCatalogue"/> holds, each under the kind the catalogue gives it.
/// </summary>
/// <remarks>
/// The index is what a consumer reads to learn what a code means and whether it can cost a test,
/// so a code missing from it is undocumented, one the catalogue no longer holds is a promise
/// about nothing, and one under the wrong kind tells a pipeline gating on <c>blind-spot</c> the
/// wrong thing. It would have caught the two codes Reach did not emit and the one it cut.
/// </remarks>
public sealed partial class NoticeIndexParityTests
{
    private const string Section = "## The notice-code index";

    [Fact]
    public void The_index_lists_every_code_Reach_can_emit_under_its_kind() =>
        Assert.Empty(Drift(Catalogue(), Indexed(Schema())));

    [Fact]
    public void One_renamed_code_in_the_index_fails_the_comparison()
    {
        // Asserted by construction rather than by trusting the test above, in both directions:
        // the code the catalogue holds goes missing, and the misspelt one is extra.
        var perturbed = Schema().Replace(
            "| `recompilation-widening` |",
            "| `recompilation-widenings` |",
            StringComparison.Ordinal);

        var drift = Drift(Catalogue(), Indexed(perturbed));

        Assert.Equal(
            [
                "`recompilation-widening` is in NoticeCatalogue as `widening` but not in the index.",
                "`recompilation-widenings` is in the index but not in NoticeCatalogue.",
            ],
            drift);
    }

    [Fact]
    public void A_code_under_the_wrong_kind_fails_the_comparison()
    {
        var reclassified = new Dictionary<string, string>(Catalogue(), StringComparer.Ordinal)
        {
            ["parse-failed"] = "widening",
        };

        var drift = Drift(reclassified, Indexed(Schema()));

        Assert.Equal(
            ["`parse-failed` is listed under `blind-spot`, but NoticeCatalogue's kind is `widening`."],
            drift);
    }

    /// <summary>
    /// Each code with its kind as the report spells it, which is the spelling the index uses.
    /// </summary>
    private static Dictionary<string, string> Catalogue() =>
        NoticeCatalogue.All.ToDictionary(
            entry => entry.Key,
            entry => JsonNamingPolicy.KebabCaseLower.ConvertName(entry.Value.ToString()),
            StringComparer.Ordinal);

    private static List<string> Drift(
        IReadOnlyDictionary<string, string> catalogue,
        IReadOnlyList<(string Code, string Kind)> indexed)
    {
        var drift = new List<string>();

        foreach (var duplicate in indexed.GroupBy(row => row.Code, StringComparer.Ordinal).Where(rows => rows.Count() > 1))
        {
            drift.Add($"`{duplicate.Key}` is in the index more than once.");
        }

        var index = indexed
            .DistinctBy(row => row.Code, StringComparer.Ordinal)
            .ToDictionary(row => row.Code, row => row.Kind, StringComparer.Ordinal);

        drift.AddRange(catalogue.Keys
            .Union(index.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(code => Describe(code, catalogue.GetValueOrDefault(code), index.GetValueOrDefault(code)))
            .OfType<string>());

        return drift;
    }

    /// <summary>
    /// What is wrong with one code, given its kind on each side (<see langword="null"/> where
    /// that side lacks it), or <see langword="null"/> when nothing is.
    /// </summary>
    private static string? Describe(string code, string? catalogueKind, string? indexKind) =>
        (catalogueKind, indexKind) switch
        {
            (_, null) => $"`{code}` is in NoticeCatalogue as `{catalogueKind}` but not in the index.",
            (null, _) => $"`{code}` is in the index but not in NoticeCatalogue.",
            _ when !string.Equals(catalogueKind, indexKind, StringComparison.Ordinal) =>
                $"`{code}` is listed under `{indexKind}`, but NoticeCatalogue's kind is `{catalogueKind}`.",
            _ => null,
        };

    /// <summary>
    /// The index is one <c>###</c> heading per kind, its kind first in backticks, each over a
    /// table whose rows start with a code in backticks. The section ends at the next
    /// <c>##</c> heading, or the end of the document.
    /// </summary>
    private static List<(string Code, string Kind)> Indexed(string schema)
    {
        var lines = schema.Split('\n');
        var start = Array.IndexOf(lines, Section);

        Assert.True(start >= 0, $"docs/report-schema.md has no \"{Section}\" heading.");

        var rows = new List<(string Code, string Kind)>();
        string? kind = null;

        foreach (var line in lines.Skip(start + 1).TakeWhile(line => !line.StartsWith("## ", StringComparison.Ordinal)))
        {
            if (KindHeading.Match(line) is { Success: true } heading)
            {
                kind = heading.Groups[1].Value;
                continue;
            }

            if (CodeRow.Match(line) is { Success: true } row && kind is not null)
            {
                rows.Add((row.Groups[1].Value, kind));
            }
        }

        // Otherwise a reshaped index passes by having nothing to compare.
        Assert.NotEmpty(rows);

        return rows;
    }

    [GeneratedRegex(@"^### `([a-z][a-z-]*)`")]
    private static partial Regex KindHeading { get; }

    [GeneratedRegex(@"^\| `([a-z][a-z0-9-]*)` \|")]
    private static partial Regex CodeRow { get; }

    private static string Schema() => RepositoryRoot.ReadText("docs", "report-schema.md");
}
