using System.Runtime.CompilerServices;
using System.Text.Json;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Acceptance;

/// <summary>
/// The committed example report, which <c>docs/report-schema.md</c> walks end to end.
/// </summary>
/// <remarks>
/// There is deliberately no JSON Schema file: the contract is tolerant — unknown fields ignored,
/// selection sections optional, <c>data</c> free-form — and a schema document would have to be
/// permissive everywhere the promise lives, then be held in sync with the code forever. A
/// committed example that <em>is</em> the determinism fixture's output costs one file and cannot
/// rot, because this test fails the moment it does.
/// <para>
/// When the change is intended, run the suite once with <c>REACH_UPDATE_EXAMPLE=1</c> and commit
/// the rewritten file.
/// </para>
/// </remarks>
public sealed partial class ExampleReportTests : IDisposable
{
    private readonly FixtureSolution fixture;
    private readonly string baseline;

    public ExampleReportTests() => fixture = FixtureSolution.Build(out baseline);

    [Fact]
    public async Task The_committed_example_is_what_the_determinism_fixture_produces()
    {
        var path = fixture.ReadFile("src/Contoso.Core/Invoice.cs");

        fixture.WriteFile(
            "src/Contoso.Core/Invoice.cs",
            path.Replace(
                "public int Tax(int net) => net * StandardRate / 100;",
                "public int Tax(int net) => (net * StandardRate / 100) + 0;",
                StringComparison.Ordinal));

        fixture.Rebuild();

        var report = await fixture.SelectAsync(baseline, TestContext.Current.CancellationToken);
        var actual = Stabilise(ReportWriter.Serialise(report), fixture.Path);

        AssertExample(actual);
    }

    /// <summary>
    /// Replaces the values that are true of one machine rather than of one input: the timings,
    /// the SHAs and the tool version. Everything the documentation actually walks survives.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex("[0-9a-f]{40}")]
    private static partial System.Text.RegularExpressions.Regex Sha();

    private static string Stabilise(string json, string fixtureRoot)
    {
        // The invocation argv and the notice messages carry the temporary directory and the
        // commit the test just made. Neither is a property of the input the example
        // illustrates, and both would make the committed file unreadable.
        json = json
            .Replace(JsonSerializer.Serialize(fixtureRoot).Trim('"'), "/repo", StringComparison.Ordinal)
            .Replace(fixtureRoot, "/repo", StringComparison.Ordinal)

            // The separators come from whichever machine ran the fixture; the example is read
            // on every other one.
            .Replace("/repo\\\\", "/repo/", StringComparison.Ordinal)
            .Replace("\\\\", "/", StringComparison.Ordinal);

        json = Sha().Replace(json, "0000000000000000000000000000000000000000");

        using var document = JsonDocument.Parse(json);

        var root = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            root[property.Name] = property.Name == "envelope"
                ? StabiliseEnvelope(property.Value)
                : property.Value;
        }

        return JsonSerializer.Serialize(
            root,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }).ReplaceLineEndings("\n");
    }

    /// <summary>
    /// Only the two the SHA pass cannot reach: the version of the tool that ran, and the clock.
    /// <strong>The baseline is deliberately left alone.</strong> Substituting a plausible-looking
    /// one — a branch reference, a detection source, <c>isHead: false</c> — would put the example
    /// in contradiction with the <c>baseline-is-head</c> notice sitting beside it, and the
    /// document walks that contradiction.
    /// </summary>
    private static Dictionary<string, object?> StabiliseEnvelope(JsonElement envelope)
    {
        var stabilised = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var property in envelope.EnumerateObject())
        {
            stabilised[property.Name] = property.Name switch
            {
                "toolVersion" => "0.1.0-alpha.1",
                "timings" => new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["startedUtc"] = "2026-01-01T00:00:00.0000000+00:00",
                    ["totalMs"] = 0,
                    ["phases"] = new Dictionary<string, int>(StringComparer.Ordinal),
                },
                _ => property.Value,
            };
        }

        return stabilised;
    }

    private static void AssertExample(string actual, [CallerFilePath] string testFile = "")
    {
        var path = Path.Combine(RepositoryRoot(testFile), "docs", "example-report.json");

        if (Environment.GetEnvironmentVariable("REACH_UPDATE_EXAMPLE") is { Length: > 0 })
        {
            File.WriteAllText(path, actual + "\n");
            return;
        }

        Assert.True(
            File.Exists(path),
            $"No example report at {path}. Run with REACH_UPDATE_EXAMPLE=1 to write one.");

        Assert.Equal(File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd(), actual.TrimEnd());
    }

    private static string RepositoryRoot(string testFile)
    {
        var directory = Path.GetDirectoryName(testFile);

        while (directory is not null && !Directory.Exists(Path.Combine(directory, ".git")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        return directory;
    }

    public void Dispose() => fixture.Dispose();
}
