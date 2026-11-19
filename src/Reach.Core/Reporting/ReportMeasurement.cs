using System.Globalization;
using Reach.Rendering;
using Reach.Selection;

namespace Reach.Reporting;

/// <summary>
/// The number that decides whether Reach is worth building further.
/// </summary>
/// <param name="WillRun">
/// <strong>The numerator is tests that will run, not tests selected.</strong> A dialect-level
/// over-match <em>is</em> over-selection, so counting the canonical selection would flatter the
/// ratio by exactly the amount one named decision costs.
/// </param>
/// <param name="Selected">The canonical selection, carried alongside so the gap is visible.</param>
/// <param name="Total">
/// Enumerable test methods in in-scope test projects. Unweighted by duration, which is the more
/// honest instrument and is unavailable because Reach does not run tests and persists nothing
/// between runs.
/// </param>
/// <param name="ProjectsRunInFull">
/// Projects on an unrecognised framework. They contribute to <em>neither</em> side — treating an
/// unenumerable project as zero would flatter the ratio in exactly the case where Reach is
/// running an entire project, which is the one case where the number most needs to be ugly.
/// </param>
/// <param name="WidenedPairs">
/// (test, change) pairs whose path class is <c>widened</c>. A test with both a compiled and a
/// widened path reads <c>compiled</c>, so this <em>understates</em> widening's contribution — a
/// registered measurement limitation, with a second traversal as the upgrade path.
/// </param>
public sealed record ReportMeasurement(
    int Selected,
    int WillRun,
    int Total,
    int ProjectsRunInFull,
    int WidenedPairs)
{
    /// <summary>
    /// The ratio's precision in the report: hundredths of a percent.
    /// </summary>
    private const int RatioDecimals = 4;

    /// <summary>
    /// A ratio of one is a hundred percent.
    /// </summary>
    private const int PercentPerUnit = 100;

    /// <summary>
    /// Null when nothing was enumerable, which is reported as <c>unknown</c> rather than as a
    /// zero denominator.
    /// </summary>
    public double? Ratio => Total > 0 ? Math.Round(Fraction(WillRun, Total), RatioDecimals, MidpointRounding.ToEven) : null;

    internal static ReportMeasurement Of(RenderedSelection rendered)
    {
        var enumerable = rendered.Entries
            .Where(entry => entry.Selection.Total is not null)
            .ToArray();

        return new ReportMeasurement(
            enumerable.Sum(entry => entry.Selection.Selected.Count),
            enumerable.Sum(entry => entry.WillRun),
            enumerable.Sum(entry => entry.Selection.Total ?? 0),
            rendered.Entries.Count(entry => entry.Selection.Total is null),
            enumerable.Sum(WidenedPairsIn));
    }

    /// <summary>
    /// Counted over pairs, not over tests, because one change reaching a test only through
    /// widening is the unit the measurement is about.
    /// </summary>
    private static int WidenedPairsIn(RenderedEntry entry) =>
        entry.Selection.Selected
            .Where(test => test.Class == PathClass.Widened)
            .Sum(test => test.Changes.Count);

    /// <summary>
    /// Floating-point division: both counts widen to <see cref="double"/> on the way in.
    /// </summary>
    private static double Fraction(double part, double whole) => part / whole;

    /// <summary>
    /// The line a person reads. Two numbers, never one — no estimated denominator and no
    /// silent zero. Culture-invariant, so every host reads the same line; the percentage is a
    /// rounded whole number and a literal <c>%</c>, because even the invariant culture's
    /// <c>P0</c> pattern inserts a space.
    /// </summary>
    internal string Describe()
    {
        var head = Ratio is { } ratio
            ? string.Create(
                CultureInfo.InvariantCulture,
                $"selected {WillRun:N0} of {Total:N0} tests ({Percent(ratio)}%)")
            : string.Create(CultureInfo.InvariantCulture, $"selected {WillRun:N0} tests of an unknown total");

        return ProjectsRunInFull == 0
            ? head
            : string.Create(
                CultureInfo.InvariantCulture,
                $"{head} · {ProjectsRunInFull} project(s) on unrecognised frameworks run in full");
    }

    /// <summary>
    /// Midpoints round to even, as ADR-0016 prescribes and as the <c>P0</c> specifier this
    /// replaces did on .NET 10: one of eight is <c>12%</c>, not <c>13%</c>.
    /// </summary>
    private static double Percent(double ratio) =>
        Math.Round(ratio * PercentPerUnit, MidpointRounding.ToEven);
}
