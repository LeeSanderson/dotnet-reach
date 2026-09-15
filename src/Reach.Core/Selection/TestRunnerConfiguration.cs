using Reach.Reporting;

namespace Reach.Selection;

/// <summary>
/// Detects the one repository misconfiguration that makes every invocation Reach emits
/// unrunnable.
/// </summary>
/// <remarks>
/// <c>xunit.v3</c> 4.0.0 resolves to <c>xunit.v3.mtp-v2</c>, and on the .NET 10 SDK that
/// package's MSBuild targets make <c>dotnet test</c> a <strong>hard build error</strong> unless
/// <c>global.json</c> carries the runner setting. The error names nothing Reach-shaped, so
/// someone will spend an afternoon on it.
/// <para>
/// <strong>Reach still emits the invocations and does not stop.</strong> The rendering is
/// correct; the repository is misconfigured. Both halves are free to detect — the adapter is in
/// the assembly's referenced identities, and <c>global.json</c> is a root file the rule table
/// already reads.
/// </para>
/// </remarks>
internal static class TestRunnerConfiguration
{
    private const string Setting = """
                                     "test": {
                                       "runner": "Microsoft.Testing.Platform"
                                     }
                                   """;

    /// <summary>
    /// A notice when a project needs the runner setting and the repository does not have it,
    /// otherwise null.
    /// </summary>
    internal static Notice? Check(
        IEnumerable<string> projectsNeedingTheRunner,
        string repositoryRoot)
    {
        var projects = projectsNeedingTheRunner.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        if (projects.Length == 0 || IsConfigured(repositoryRoot))
        {
            return null;
        }

        return new Notice(
            NoticeCodes.TestRunnerNotConfigured,
            NoticeKind.Environment,
            $"{string.Join(", ", projects)} use a test framework that requires the "
            + "Microsoft.Testing.Platform runner, and this repository's global.json does not "
            + "set it — so `dotnet test` is a hard build error and every command Reach emitted "
            + "is unrunnable. Add to global.json:\n\n"
            + Setting,
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["projects"] = projects });
    }

    /// <summary>Whether <c>global.json</c> carries the runner setting at all.</summary>
    internal static bool IsConfigured(string repositoryRoot)
    {
        var path = Path.Combine(repositoryRoot, "global.json");

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

            return document.RootElement.TryGetProperty("test", out var test)
                && test.TryGetProperty("runner", out var runner)
                && runner.GetString() is { Length: > 0 };
        }
        catch (System.Text.Json.JsonException)
        {
            // Unreadable reads as unconfigured, which produces a notice rather than silence.
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Which dialects need it. The adapter arrives with <c>xunit.v3</c> 4.0.0 and nothing else
    /// in M1's table.
    /// </summary>
    internal static bool Needs(string? dialect) =>
        dialect is not null && dialect.StartsWith("xunit-v3-4", StringComparison.Ordinal);
}
