namespace Reach;

/// <summary>
/// One <c>reach select</c> invocation, parsed. Every field is a command-line option or a
/// fact about where the process is running; nothing here is derived.
/// </summary>
/// <remarks>
/// No field may narrow scope. There is deliberately no test-project include or exclude, no
/// target-framework restriction and no <c>--no-untracked</c>: a switch whose only possible
/// effect is under-selection is a poor use of the option budget.
/// </remarks>
internal sealed record SelectRequest
{
    /// <summary>The solution or single test project. Discovered in the working directory when absent.</summary>
    internal string? Target { get; init; }

    /// <summary>The target branch. Auto-detected when absent.</summary>
    internal string? Base { get; init; }

    internal string? Configuration { get; init; }

    /// <summary>Build output. Never means "where Reach writes" — that is <see cref="ReportDirectory"/>.</summary>
    internal string? Output { get; init; }

    internal string? ArtifactsPath { get; init; }

    internal bool NoBuild { get; init; }

    /// <summary>A path, or <c>-</c> for standard output. Moves the JSON alone.</summary>
    internal string? Report { get; init; }

    /// <summary>Moves the whole directory Reach owns, side-car files included.</summary>
    internal string? ReportDirectory { get; init; }

    internal bool Paths { get; init; }

    internal bool ListUnselected { get; init; }

    /// <summary>
    /// Documented by its effect — "merge Reach's filter into this runsettings file and write
    /// the result" — because the name is a foot-gun: someone will pass it expecting Reach to
    /// <em>use</em> their settings for a test run Reach never performs.
    /// </summary>
    internal string? RunSettings { get; init; }

    internal Verbosity Verbosity { get; init; } = Verbosity.Normal;

    internal bool NoColor { get; init; }

    /// <summary>
    /// Everything after <c>--</c>, forwarded verbatim to <c>dotnet build</c>. Recorded in the
    /// report envelope so a surprising run stays reproducible.
    /// </summary>
    internal IReadOnlyList<string> ForwardedBuildArguments { get; init; } = [];

    internal string WorkingDirectory { get; init; } = Environment.CurrentDirectory;
}
