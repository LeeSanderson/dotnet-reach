namespace Reach.Tests.Acceptance;

/// <summary>
/// What an acceptance test says Reach must report, written by hand. Never generated from
/// Reach's output: the point is that a human said what <em>should</em> be selected.
/// </summary>
/// <remarks>
/// <para>
/// The four <c>required</c> members are what the compiler checks. <see cref="ExitCode"/> is
/// required exactly when <see cref="Outcome"/> is <see cref="Outcome.Failed"/>, which the
/// compiler cannot express, so <c>Expect</c> checks it when it runs.
/// </para>
/// <para>
/// <strong>An optional section that is given is exact</strong>: a notice nobody expected fails
/// the test. Left out means not checked.
/// </para>
/// <para>
/// Test projects are keyed by the <c>.csproj</c> file name without its extension. A
/// multi-targeted project's entries are written <c>Name@tfm</c>; a bare name that matches more
/// than one entry is an error, so a framework cannot be skipped by accident.
/// </para>
/// </remarks>
internal sealed record Expected
{
    /// <summary>
    /// One sentence: why exactly these tests. Printed first in every failure.
    /// </summary>
    public required string Because { get; init; }

    /// <summary>
    /// The report's outcome.
    /// </summary>
    public required Outcome Outcome { get; init; }

    /// <summary>
    /// Test-project key to the full display names of its selected tests. A project not listed
    /// has nothing selected.
    /// </summary>
    public required Dictionary<string, IReadOnlyList<string>> Selected { get; init; }

    /// <summary>
    /// The test-project keys run in full by whole-project fallback.
    /// </summary>
    public required IReadOnlyList<string> RunInFull { get; init; }

    /// <summary>
    /// The exit code a failed run must return. Given exactly when <see cref="Outcome"/> is
    /// <see cref="Outcome.Failed"/>.
    /// </summary>
    public int? ExitCode { get; init; }

    /// <summary>
    /// Test-project key to the rendered filter's true match count. A project not listed will
    /// run nothing.
    /// </summary>
    public Dictionary<string, int>? WillRun { get; init; }

    /// <summary>
    /// The set of notice codes.
    /// </summary>
    public IReadOnlyList<string>? Notices { get; init; }

    /// <summary>
    /// The set of reason codes, which only a <c>nothing-selected</c> report carries.
    /// </summary>
    public IReadOnlyList<string>? Reasons { get; init; }

    /// <summary>
    /// Changed member display to the number of tests it reached.
    /// </summary>
    public Dictionary<string, int>? Changes { get; init; }

    /// <summary>
    /// Changed member display to the tier it was routed at.
    /// </summary>
    public Dictionary<string, Tier>? Tiers { get; init; }
}

/// <summary>
/// A report's outcome, as an acceptance test writes it.
/// </summary>
internal enum Outcome
{
    Selected,
    NothingSelected,
    NoChanges,
    Failed,
}

/// <summary>
/// The granularity a change was routed at, as an acceptance test writes it.
/// </summary>
internal enum Tier
{
    Member,
    WholeType,
    WholeAssembly,
}

/// <summary>
/// The spellings <c>report.json</c> uses, written out by hand rather than borrowed from Reach,
/// so the expected side of every comparison is independent of the code it checks.
/// </summary>
internal static class Wire
{
    internal static string Of(Outcome outcome) => outcome switch
    {
        Outcome.Selected => "selected",
        Outcome.NothingSelected => "nothing-selected",
        Outcome.NoChanges => "no-changes",
        Outcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null),
    };

    internal static string Of(Tier tier) => tier switch
    {
        Tier.Member => "member",
        Tier.WholeType => "whole-type",
        Tier.WholeAssembly => "whole-assembly",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, null),
    };
}
