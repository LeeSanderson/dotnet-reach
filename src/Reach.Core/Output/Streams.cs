
namespace Reach.Output;

/// <summary>
/// Where each of Reach's three outputs goes: the human summary, the notices and logs, and the
/// JSON report.
/// </summary>
/// <remarks>
/// The summary is on standard output and the notices on standard error — until
/// <c>--report -</c>, which puts the JSON on standard output and moves the summary across, so
/// that a consumer piping Reach into a parser never has to strip prose out of it.
/// </remarks>
public sealed class Streams
{
    private Streams(TextWriter summary, TextWriter notices, TextWriter? report, bool colour)
    {
        Summary = summary;
        Notices = notices;
        Report = report;
        Colour = colour;
    }

    public TextWriter Summary { get; }

    public TextWriter Notices { get; }

    /// <summary>
    /// Non-null only when the report goes to a stream rather than a file.
    /// </summary>
    internal TextWriter? Report { get; }

    internal bool Colour { get; }

    public static Streams For(
        ReportDestination destination,
        TextWriter standardOutput,
        TextWriter standardError,
        bool colour) =>
        destination.ToStandardOutput
            ? new Streams(standardError, standardError, standardOutput, colour)
            : new Streams(standardOutput, standardError, null, colour);
}

/// <summary>
/// Whether to colour the summary. Auto-detected, with two ways to say no.
/// </summary>
public static class ColourSupport
{
    public static bool Enabled(bool noColorOption, EnvironmentLookup environment, bool outputIsRedirected)
    {
        if (noColorOption || outputIsRedirected)
        {
            return false;
        }

        // no-color.org: present and not an empty string, whatever its value.
        return string.IsNullOrEmpty(environment("NO_COLOR"));
    }
}
