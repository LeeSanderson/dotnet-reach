using Reach.Reporting;

namespace Reach.Baselines;

/// <summary>
/// What baseline resolution concluded: a <see cref="Baselines.Baseline"/>, or a message that
/// fixes the problem in one line.
/// </summary>
internal sealed record BaselineResolution
{
    private BaselineResolution(Baseline? baseline, string message, IReadOnlyList<Notice> notices)
    {
        Baseline = baseline;
        Message = message;
        Notices = notices;
    }

    internal Baseline? Baseline { get; }

    /// <summary>
    /// On failure, the product surface: what went wrong and the literal line to add. Reach does
    /// not work under default CI settings, so this message is the likeliest thing an adopter
    /// ever reads from it.
    /// </summary>
    internal string Message { get; }

    internal IReadOnlyList<Notice> Notices { get; }

    internal bool Resolved => Baseline is not null;

    internal ExitCode ExitCode => Resolved ? ExitCode.Success : ExitCode.BaselineUnresolvable;

    internal static BaselineResolution From(Baseline baseline, IReadOnlyList<Notice> notices) =>
        new(baseline, string.Empty, notices);

    internal static BaselineResolution Unresolvable(string message, IReadOnlyList<Notice> notices) =>
        new(null, message, notices);
}
