using System.Text.Encodings.Web;
using System.Text.Json;
using Reach.Output;

namespace Reach.Reporting;

/// <summary>Serialises the report, and puts it where the command line said.</summary>
internal static class ReportWriter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,

        // The report carries filter fragments and type names. HTML-escaping them would make
        // the document unreadable for the one person it exists for.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static string Serialise(Report report) =>
        JsonSerializer.Serialize(report, Options).ReplaceLineEndings("\n");

    /// <summary>
    /// Writes the report and returns where it went, or null when it went to a stream.
    /// </summary>
    internal static string? Write(Report report, ReportDestination destination, Streams streams)
    {
        var json = Serialise(report);

        if (destination.ToStandardOutput)
        {
            streams.Report!.Write(json);
            streams.Report.Write('\n');
            return null;
        }

        var path = destination.Path!;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json + "\n");

        return path;
    }
}
