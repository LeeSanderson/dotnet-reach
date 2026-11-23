using System.Text;
using System.Text.Json.Nodes;
using Reach.Reporting;

namespace Reach.Tests.Acceptance;

/// <summary>
/// Compares an <see cref="Expected"/> with what was observed, and checks the invariants every
/// report holds whatever the patch.
/// </summary>
/// <remarks>
/// <strong>Every difference is reported at once</strong>, section by section, with
/// <see cref="Expected.Because"/> first: a test that stops at the first difference makes its
/// author fix the expected result one round trip at a time. <strong>It never prints a pasteable
/// expected literal</strong>: expected results are written by hand, and a literal to paste would
/// turn every failure into an approval step.
/// </remarks>
internal static class Expectation
{
    private const string RunAll = "run-all";

    /// <summary>
    /// The failure message, or null when the observation is what was expected.
    /// </summary>
    internal static string? Failures(Expected expected, Observation observed)
    {
        var report = observed.First.Report;
        var keys = new EntryKeys(report.Entries);

        (string Name, IReadOnlyList<string> Differences)[] sections =
        [
            ("Outcome", OutcomeDifferences(expected, report)),
            ("Selected", SelectedDifferences(expected, keys)),
            ("RunInFull", RunInFullDifferences(expected, keys)),
            ("ExitCode", ExitCodeDifferences(expected)),
            ("WillRun", expected.WillRun is { } willRun ? WillRunDifferences(willRun, keys) : []),
            ("Notices", expected.Notices is { } notices ? SetDifferences(notices, report.Notices.Select(notice => notice.Code)) : []),
            ("Reasons", expected.Reasons is { } reasons ? SetDifferences(reasons, report.Reasons ?? []) : []),
            ("Changes", expected.Changes is { } changes ? ChangeDifferences(changes, report, change => Number(change.TestsReached), Number) : []),
            ("Tiers", expected.Tiers is { } tiers ? ChangeDifferences(tiers, report, change => change.Tier, Wire.Of) : []),
            ("Invariant, determinism", Determinism(observed)),
            ("Invariant, exit code matches outcome", ExitCodeMatchesOutcome(expected, observed)),
            ("Invariant, skip means nothing runs", SkipMeansNothingRuns(keys)),
            ("Invariant, unknown is never zero", UnknownIsNeverZero(report, keys)),
        ];

        var failing = sections.Where(section => section.Differences.Count > 0).ToArray();

        if (failing.Length == 0)
        {
            return null;
        }

        var message = new StringBuilder().Append("Because: ").Append(expected.Because).Append('\n');

        foreach (var (name, differences) in failing)
        {
            message.Append('\n').Append(name).Append(":\n");

            foreach (var difference in differences)
            {
                message.Append("  ").Append(difference).Append('\n');
            }
        }

        if (observed.BuildFailure is { } tail)
        {
            message
                .Append("\nThe harness's own build failed, so Reach ran in build mode. The build's last lines:\n")
                .Append(tail)
                .Append('\n');
        }

        return message.ToString();
    }

    private static List<string> OutcomeDifferences(Expected expected, Report report) =>
        Wire.Of(expected.Outcome) == report.Outcome
            ? []
            : [$"expected {Wire.Of(expected.Outcome)}, was {report.Outcome}"];

    private static List<string> SelectedDifferences(Expected expected, EntryKeys keys)
    {
        var differences = new List<string>();
        var listed = new HashSet<ReportEntry>(ReferenceEqualityComparer.Instance);

        foreach (var (key, tests) in expected.Selected.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (keys.Resolve(key, differences) is not { } entry)
            {
                continue;
            }

            listed.Add(entry);

            var actual = entry.Tests.Select(test => test.Display).ToArray();

            differences.AddRange(tests.Except(actual).Order(StringComparer.Ordinal).Select(test => $"{key}: not selected {test}"));
            differences.AddRange(actual.Except(tests).Order(StringComparer.Ordinal).Select(test => $"{key}: selected but not expected {test}"));
        }

        foreach (var entry in keys.Entries.Where(entry => !listed.Contains(entry)))
        {
            differences.AddRange(entry.Tests.Select(test => $"{keys.KeyOf(entry)}: selected but not expected {test.Display}"));
        }

        return differences;
    }

    private static List<string> RunInFullDifferences(Expected expected, EntryKeys keys)
    {
        var differences = new List<string>();
        var listed = new HashSet<ReportEntry>(ReferenceEqualityComparer.Instance);

        foreach (var key in expected.RunInFull.Order(StringComparer.Ordinal))
        {
            if (keys.Resolve(key, differences) is not { } entry)
            {
                continue;
            }

            listed.Add(entry);

            if (entry.Mode != RunAll)
            {
                differences.Add($"{key}: expected to run in full, mode was {entry.Mode}");
            }
        }

        differences.AddRange(keys.Entries
            .Where(entry => entry.Mode == RunAll && !listed.Contains(entry))
            .Select(entry => $"{keys.KeyOf(entry)}: runs in full, not expected"));

        return differences;
    }

    private static List<string> ExitCodeDifferences(Expected expected) =>
        (expected.Outcome, expected.ExitCode) switch
        {
            (Outcome.Failed, null) => ["required when Outcome is Failed"],
            (not Outcome.Failed, not null) => [$"given for outcome {Wire.Of(expected.Outcome)}; only Failed takes one"],
            _ => [],
        };

    private static List<string> WillRunDifferences(Dictionary<string, int> willRun, EntryKeys keys)
    {
        var differences = new List<string>();
        var listed = new HashSet<ReportEntry>(ReferenceEqualityComparer.Instance);

        foreach (var (key, count) in willRun.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (keys.Resolve(key, differences) is not { } entry)
            {
                continue;
            }

            listed.Add(entry);

            if (entry.Counts.WillRun != count)
            {
                differences.Add($"{key}: expected {Number(count)}, was {Number(entry.Counts.WillRun)}");
            }
        }

        differences.AddRange(keys.Entries
            .Where(entry => entry.Counts.WillRun != 0 && !listed.Contains(entry))
            .Select(entry => $"{keys.KeyOf(entry)}: will run {Number(entry.Counts.WillRun)}, not listed"));

        return differences;
    }

    private static List<string> SetDifferences(IEnumerable<string> expected, IEnumerable<string> actual)
    {
        var wanted = expected.ToHashSet(StringComparer.Ordinal);
        var got = actual.ToHashSet(StringComparer.Ordinal);

        return
        [
            .. wanted.Except(got).Order(StringComparer.Ordinal).Select(code => $"missing {code}"),
            .. got.Except(wanted).Order(StringComparer.Ordinal).Select(code => $"not expected {code}"),
        ];
    }

    /// <summary>
    /// Compares one value per changed member display, for the <c>Changes</c> and <c>Tiers</c>
    /// sections alike.
    /// </summary>
    private static List<string> ChangeDifferences<T>(
        Dictionary<string, T> expected,
        Report report,
        Func<ReportChange, string> valueOf,
        Func<T, string> spell)
    {
        var actual = report.Changes
            .GroupBy(change => change.Display, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(valueOf)), StringComparer.Ordinal);

        var differences = new List<string>();

        foreach (var (display, value) in expected.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var wanted = spell(value);
            var got = actual.GetValueOrDefault(display);

            if (got != wanted)
            {
                differences.Add(got is null
                    ? $"{display}: expected, not changed"
                    : $"{display}: expected {wanted}, was {got}");
            }
        }

        differences.AddRange(actual.Keys
            .Where(display => !expected.ContainsKey(display))
            .Order(StringComparer.Ordinal)
            .Select(display => $"{display}: changed, not expected"));

        return differences;
    }

    /// <summary>
    /// The two reports are identical once the values true of one run rather than of one input
    /// are scrubbed.
    /// </summary>
    private static IReadOnlyList<string> Determinism(Observation observed)
    {
        var first = ReportScrubber.Scrub(ReportWriter.Serialise(observed.First.Report), observed.TempRoot);
        var second = ReportScrubber.Scrub(ReportWriter.Serialise(observed.Second.Report), observed.TempRoot);

        return first == second
            ? []
            : ["the two reports differ after scrubbing (- run 1, + run 2):", .. LineDiff.Of(first, second)];
    }

    /// <summary>
    /// <c>selected</c>, <c>nothing-selected</c> and <c>no-changes</c> exit 0; <c>failed</c> exits
    /// the code the test expects.
    /// </summary>
    private static List<string> ExitCodeMatchesOutcome(Expected expected, Observation observed) =>
    [
        .. new[] { (Number: 1, Run: observed.First), (Number: 2, Run: observed.Second) }
            .Select(pair => ExitCodeDifference(pair.Number, pair.Run, expected.ExitCode))
            .OfType<string>(),
    ];

    private static string? ExitCodeDifference(int number, ReachRun run, int? expectedCode)
    {
        var outcome = run.Report.Outcome;
        var prefix = $"run {Number(number)} exited {Number(run.ExitCode)} with outcome {outcome}";

        return (outcome == Wire.Of(Outcome.Failed), expectedCode) switch
        {
            (false, _) when run.ExitCode != 0 => $"{prefix}, which exits 0",
            (true, null) => $"{prefix}, and no ExitCode is expected",
            (true, { } code) when run.ExitCode != code => $"{prefix}; expected {Number(code)}",
            _ => null,
        };
    }

    private static List<string> SkipMeansNothingRuns(EntryKeys keys) =>
    [
        .. keys.Entries
            .Where(entry => entry.Tests.Count == 0 && entry.Mode != RunAll)
            .Where(entry => entry.Mode != "skip" || entry.Invocations.Count != 0)
            .Select(entry =>
                $"{keys.KeyOf(entry)}: nothing selected, mode {entry.Mode}, {Number(entry.Invocations.Count)} invocation(s)"),
    ];

    /// <summary>
    /// A total Reach could not count serialises as <c>"unknown"</c>, and a project run in full
    /// never serialises a total of <c>0</c>, which is how an unknown total would read if it
    /// leaked. Read from the serialised report, because the claim is about what a consumer reads.
    /// </summary>
    /// <remarks>
    /// A project run in full may still have a known total: one whose framework selector is
    /// underivable (row 10) is analysed like any other and only rendered project-wide.
    /// </remarks>
    private static List<string> UnknownIsNeverZero(Report report, EntryKeys keys)
    {
        var entries = JsonNode.Parse(ReportWriter.Serialise(report))!["entries"]!.AsArray();
        var differences = new List<string>();

        for (var index = 0; index < entries.Count; index++)
        {
            if (TotalDifference(report.Entries[index], entries[index]!["counts"]!["total"]!) is { } difference)
            {
                differences.Add($"{keys.KeyOf(report.Entries[index])}: {difference}");
            }
        }

        return differences;
    }

    private static string? TotalDifference(ReportEntry entry, JsonNode total)
    {
        if (total.GetValueKind() == System.Text.Json.JsonValueKind.String && total.GetValue<string>() == "unknown")
        {
            return null;
        }

        return (entry.Counts.Total, entry.Mode) switch
        {
            (null, _) => $"total not counted, serialised as {total.ToJsonString()}",
            (0, RunAll) => "runs in full, total serialised as 0",
            _ => null,
        };
    }

    private static string Number(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
