using System.Text.RegularExpressions;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// The fenced YAML in <c>docs/adopting-reach.md</c> is byte-for-byte the workflow this
/// repository runs on its own pull requests.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The one carve-out in an otherwise judged adoption criterion.</strong> Reach does not
/// work under a provider's default checkout settings, so exit 4 is the likeliest first run
/// anybody has, and its fix is one line of YAML in that block. A wrong <c>fetch-depth</c> there
/// breaks the under-an-hour criterion on contact, which is not something judgement catches — so
/// this one line is asserted, and everything else about adoption cost is judged.
/// </para>
/// <para>
/// It is a test in the suite rather than a CI step, and it compares against a whole workflow
/// file rather than one job of a larger one: a fenced block that matched a fragment would be a
/// worse test of a worse artifact.
/// </para>
/// </remarks>
public partial class WorkflowParityTests
{
    private const string Open = "<!-- reach:dogfood-workflow -->";
    private const string Close = "<!-- /reach:dogfood-workflow -->";

    [Fact]
    public void The_documented_recipe_is_the_workflow_this_repository_runs() =>
        Assert.Equal(Workflow(), FencedBlock());

    [Fact]
    public void One_changed_character_in_the_documented_recipe_fails_the_comparison()
    {
        // Asserted by construction rather than by trusting the test above. The character that
        // matters most is the one this whole test exists for.
        var perturbed = FencedBlock().Replace("fetch-depth: 0", "fetch-depth: 1", StringComparison.Ordinal);

        Assert.NotEqual(Workflow(), perturbed);
    }

    [Fact]
    public void The_documented_recipe_pins_the_version()
    {
        // A bare package id will not resolve a prerelease, and fails with a misleading
        // "not found in NuGet feeds". The pin is a requirement, not a preference.
        Assert.Contains("dnx dotnet-reach@", FencedBlock(), StringComparison.Ordinal);
        Assert.DoesNotContain("dnx dotnet-reach ", FencedBlock(), StringComparison.Ordinal);
    }

    [Fact]
    public void Every_documented_dnx_line_pins_the_version_this_repository_builds()
    {
        // The version is hand-edited in one file and repeated in the prose, which is the cost
        // of having no version derivation — a deliberate trade, since a published package
        // cannot be deleted. What is not acceptable is the bump landing in Directory.Build.props
        // and leaving the quickstart pointing at a release that is no longer the current one.
        var version = Version();

        foreach (var document in new[] { "README.md", Path.Combine("docs", "adopting-reach.md") })
        {
            var pins = Pins().Matches(Read(RepositoryRoot.Combine(document))).Cast<Match>().ToArray();

            // Otherwise a document that lost its quickstart passes this test by having nothing
            // to check, which is the failure it is least able to afford.
            Assert.NotEmpty(pins);
            Assert.All(pins, pinned => Assert.Equal(version, pinned.Groups[1].Value));
        }

        Assert.Equal(version, Assert.Single(Pins().Matches(Workflow()).Cast<Match>()).Groups[1].Value);
    }

    [GeneratedRegex(@"dnx dotnet-reach@([^\s`]+)")]
    private static partial Regex Pins();

    private static string Version()
    {
        var properties = Read(RepositoryRoot.Combine("Directory.Build.props"));
        var version = VersionElement().Match(properties);

        Assert.True(version.Success, "Directory.Build.props declares no <Version>.");

        return version.Groups[1].Value;
    }

    [GeneratedRegex(@"<Version>([^<]+)</Version>")]
    private static partial Regex VersionElement();

    [Fact]
    public void The_documented_recipe_fetches_the_whole_history() =>
        Assert.Contains("fetch-depth: 0", FencedBlock(), StringComparison.Ordinal);

    private static string Workflow() =>
        Read(RepositoryRoot.Combine(".github", "workflows", "dogfood.yml"));

    /// <summary>
    /// The YAML between the markers, with the fence itself removed. The markers are HTML
    /// comments, so they are invisible on GitHub and the block reads as an ordinary example.
    /// </summary>
    private static string FencedBlock()
    {
        var document = Read(RepositoryRoot.Combine("docs", "adopting-reach.md"));

        var start = document.IndexOf(Open, StringComparison.Ordinal);
        var end = document.IndexOf(Close, StringComparison.Ordinal);

        Assert.True(start >= 0, $"docs/adopting-reach.md carries no {Open} marker.");
        Assert.True(end > start, $"docs/adopting-reach.md carries no {Close} marker after the opening one.");

        var fenced = document[(start + Open.Length)..end].Trim('\n');

        Assert.StartsWith("```yaml\n", fenced, StringComparison.Ordinal);
        Assert.EndsWith("\n```", fenced, StringComparison.Ordinal);

        return fenced["```yaml\n".Length..^"\n```".Length] + "\n";
    }

    /// <summary>
    /// Line endings are normalised, and only line endings. Git hands a Windows checkout CRLF and
    /// a Linux one LF, and a test that failed on the platform rather than on the content would
    /// be reporting the wrong thing on the one line it exists to protect.
    /// </summary>
    private static string Read(string path)
    {
        Assert.True(File.Exists(path), $"Nothing at {path}.");

        return File.ReadAllText(path).ReplaceLineEndings("\n");
    }
}
