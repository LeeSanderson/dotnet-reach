namespace Reach.Output;

/// <summary>
/// Where <c>report.json</c> goes. <c>--report -</c> sends it to standard output and moves the
/// human summary to standard error, so a consumer can pipe the JSON straight into a parser.
/// </summary>
internal sealed record ReportDestination(string? Path)
{
    internal const string StandardOutputToken = "-";

    internal bool ToStandardOutput => Path is null;

    internal static ReportDestination Resolve(
        string? report,
        ReachDirectory directory,
        string workingDirectory) => report switch
        {
            StandardOutputToken => new ReportDestination(Path: null),
            { Length: > 0 } => new ReportDestination(System.IO.Path.GetFullPath(report, workingDirectory)),
            _ => new ReportDestination(directory.Combine("report.json")),
        };
}
