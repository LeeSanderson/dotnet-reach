using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Reach.Tests.Acceptance;

/// <summary>
/// Removes from a serialised report what is true of one run rather than of one input, so two
/// runs over the same input compare equal as strings.
/// </summary>
/// <remarks>
/// In-house rather than Verify: a snapshot library's approval mode would let an expected result
/// be generated from Reach's output, which is the one thing the acceptance layer forbids.
/// </remarks>
internal static class ReportScrubber
{
    internal const string TempRootToken = "<temp>";

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// Drops <c>envelope.timings</c>, which differs every run as the schema says, and replaces
    /// the temp root, in every spelling a report can carry it, with a fixed token.
    /// </summary>
    internal static string Scrub(string json, string tempRoot)
    {
        var report = JsonNode.Parse(json)!;

        report["envelope"]?.AsObject().Remove("timings");

        var scrubbed = report.ToJsonString(Indented).ReplaceLineEndings("\n");

        return scrubbed
            .Replace(JsonSerializer.Serialize(tempRoot).Trim('"'), TempRootToken, StringComparison.Ordinal)
            .Replace(tempRoot, TempRootToken, StringComparison.Ordinal)
            .Replace(tempRoot.Replace('\\', '/'), TempRootToken, StringComparison.Ordinal);
    }
}
