using System.Text;

namespace Reach.Reporting;

/// <summary>
/// What a person reads. <strong>Explicitly not a contract</strong> — the documentation says so,
/// so nobody builds a <c>grep</c> pipeline against it.
/// </summary>
/// <remarks>
/// A normal run <em>is</em> the preview: there is no dry-run verb, because <c>select</c> does
/// not run tests and a second verb would create two code paths obliged to agree. So three
/// things have to be right here, not only in the JSON.
/// </remarks>
internal static class HumanSummary
{
    internal static string Write(Report report, string? reportPath, string? reachDirectory)
    {
        var text = new StringBuilder();

        Baseline(text, report);
        Verdict(text, report);
        Entries(text, report);
        Notices(text, report);
        Destinations(text, reportPath, reachDirectory);

        return text.ToString();
    }

    /// <summary>
    /// <strong>The resolved baseline SHA and its detection source lead every run.</strong> An
    /// audit line nobody reads does not catch a baseline that resolved to <c>HEAD</c>.
    /// </summary>
    private static void Baseline(StringBuilder text, Report report)
    {
        if (report.Envelope.Baseline is not { } baseline)
        {
            text.AppendLine("No baseline was resolved.");
            return;
        }

        text.Append("Baseline ").Append(Short(baseline.Sha))
            .Append(" (merge-base with '").Append(baseline.Reference)
            .Append("', from ").Append(baseline.DetectedFrom).Append(')');

        if (baseline.IsHead)
        {
            text.Append(" — which is HEAD, so only uncommitted work can appear as a change");
        }

        text.AppendLine();
    }

    /// <summary>
    /// <c>no-changes</c> and <c>nothing-selected</c> get visibly different verdicts, not two
    /// shades of "0 tests". They exit the same and mean opposite things.
    /// </summary>
    private static void Verdict(StringBuilder text, Report report)
    {
        text.AppendLine();

        switch (report.Outcome)
        {
            case "no-changes":
                text.AppendLine("No changes to analyse. Nothing in this working tree differs from the baseline.");
                break;

            case "nothing-selected":
                // The reason codes lead: this is the result most likely to be disbelieved.
                text.AppendLine(
                    "Nothing selected: "
                    + string.Join(", ", report.Reasons ?? ["no-changed-member-reached-a-test"]));
                text.AppendLine(
                    "There WERE changes. No test could reach any of them, which is either a "
                    + "coverage gap or a bug in Reach.");
                break;

            case "failed":
                text.AppendLine("The run failed. Run the whole suite, or stop the build.");
                break;

            default:
                var selected = report.Entries.Sum(entry => entry.Counts.Selected);
                var runAll = report.Entries.Count(entry => entry.Mode == "run-all");

                text.Append("Selected ").Append(selected).Append(" test(s)");

                if (runAll > 0)
                {
                    text.Append(", and ").Append(runAll).Append(" project(s) run in full");
                }

                text.AppendLine(".");
                break;
        }
    }

    private static void Entries(StringBuilder text, Report report)
    {
        if (report.Entries.Count == 0)
        {
            return;
        }

        text.AppendLine();

        foreach (var entry in report.Entries)
        {
            text.Append("  ").Append(Name(entry.Project)).Append(" (").Append(entry.TargetFramework)
                .Append(")  ").Append(entry.Mode)
                .Append("  ").Append(entry.Counts.Selected).Append('/')
                .Append(entry.Counts.Total?.ToString() ?? "unknown")
                .AppendLine();
        }
    }

    /// <summary>
    /// Counts by kind, with every blind-spot notice printed in full: those are the ones that
    /// say Reach may have missed something, and a count alone would bury them.
    /// </summary>
    private static void Notices(StringBuilder text, Report report)
    {
        if (report.Notices.Count == 0)
        {
            return;
        }

        text.AppendLine();

        foreach (var group in report.Notices
            .GroupBy(notice => notice.Kind)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            text.Append("  ").Append(group.Count()).Append(' ').Append(group.Key).Append(" notice(s)")
                .AppendLine();
        }

        foreach (var notice in report.Notices.Where(notice => notice.Kind == "blind-spot"))
        {
            text.AppendLine();
            text.Append("  ").Append(notice.Code).Append(": ").AppendLine(notice.Message);
        }
    }

    private static void Destinations(StringBuilder text, string? reportPath, string? reachDirectory)
    {
        if (reportPath is null && reachDirectory is null)
        {
            return;
        }

        text.AppendLine();

        if (reportPath is not null)
        {
            text.Append("  report: ").AppendLine(reportPath);
        }

        if (reachDirectory is not null)
        {
            text.Append("  side-car files: ").AppendLine(reachDirectory);
        }
    }

    private static string Short(string sha) => sha.Length > 10 ? sha[..10] : sha;

    private static string Name(string path) => Path.GetFileNameWithoutExtension(path);
}
