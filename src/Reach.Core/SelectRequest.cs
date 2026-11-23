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
public sealed record SelectRequest
{
    /// <summary>
    /// The solution or single test project. Discovered in the working directory when absent.
    /// </summary>
    public string? Target { get; init; }

    /// <summary>
    /// The target branch. Auto-detected when absent.
    /// </summary>
    public string? Base { get; init; }

    public string? Configuration { get; init; }

    /// <summary>
    /// Build output. Never means "where Reach writes" — that is <see cref="ReportDirectory"/>.
    /// </summary>
    public string? Output { get; init; }

    public string? ArtifactsPath { get; init; }

    public bool NoBuild { get; init; }

    /// <summary>
    /// A path, or <c>-</c> for standard output. Moves the JSON alone.
    /// </summary>
    public string? Report { get; init; }

    /// <summary>
    /// Moves the whole directory Reach owns, side-car files included.
    /// </summary>
    public string? ReportDirectory { get; init; }

    public bool Paths { get; init; }

    /// <summary>
    /// Documented by its effect — "merge Reach's filter into this runsettings file and write
    /// the result" — because the name is a foot-gun: someone will pass it expecting Reach to
    /// <em>use</em> their settings for a test run Reach never performs.
    /// </summary>
    public string? RunSettings { get; init; }

    public Verbosity Verbosity { get; init; } = Verbosity.Normal;

    public bool NoColor { get; init; }

    /// <summary>
    /// Everything after <c>--</c>, forwarded verbatim to <c>dotnet build</c>. Recorded in the
    /// report envelope so a surprising run stays reproducible.
    /// </summary>
    public IReadOnlyList<string> ForwardedBuildArguments { get; init; } = [];

    public string WorkingDirectory { get; init; } = Environment.CurrentDirectory;
}
