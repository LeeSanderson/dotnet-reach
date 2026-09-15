using System.Text.Json;
using Reach.Output;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// The report, which is the product surface everything else is judged by.
/// </summary>
public class ReportTests
{
    private const string Core = """
        namespace N;

        public class Widget
        {
            public int Spin() => Inner();
            public int Inner() => 1;
            public int Untouched() => 2;
        }
        """;

    private const string Tests = """
        namespace N.Tests;

        public class WidgetTests
        {
            [Xunit.Fact]
            public void Spins() => new N.Widget().Spin();

            [Xunit.Fact]
            public void Untouched() => new N.Widget().Untouched();
        }
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // ---- Determinism, first, because it is what makes everything else cheap -------------------

    [Fact]
    public async Task The_same_input_twice_produces_byte_identical_json()
    {
        using var repository = ReportFixture.Repository();

        var first = await ReportFixture.RunAsync(repository, Token);
        var second = await ReportFixture.RunAsync(repository, Token);

        // With the envelope's timings excluded, two runs over the same input are identical
        // bytes. This is the assertion that makes exact expected selections the cheap option
        // rather than the brittle one.
        Assert.Equal(WithoutTimings(first), WithoutTimings(second));
    }

    [Fact]
    public async Task Only_the_timings_differ_between_two_runs()
    {
        using var repository = ReportFixture.Repository();

        var first = await ReportFixture.RunAsync(repository, Token);
        var second = await ReportFixture.RunAsync(repository, Token);

        // The segregation earns its place only if excluding that one key is enough.
        Assert.NotEqual(first, second);
        Assert.Equal(WithoutTimings(first), WithoutTimings(second));
    }

    private static string WithoutTimings(string json)
    {
        using var document = JsonDocument.Parse(json);

        var options = new JsonSerializerOptions { WriteIndented = true };
        var root = document.RootElement.Clone();

        var rewritten = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        foreach (var property in root.EnumerateObject())
        {
            rewritten[property.Name] = property.Name == "envelope"
                ? Strip(property.Value)
                : property.Value;
        }

        return JsonSerializer.Serialize(rewritten, options);
    }

    private static JsonElement Strip(JsonElement envelope)
    {
        var kept = envelope.EnumerateObject()
            .Where(property => property.Name != "timings")
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.Ordinal);

        return JsonSerializer.SerializeToElement(kept);
    }

    // ---- Shape ----------------------------------------------------------------------------------

    [Fact]
    public void A_single_targeted_project_still_produces_a_project_and_framework_keyed_entry()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections);
        var entry = Assert.Single(report.Entries);

        // Uniformly, even for a single-targeted project, so there is no special case in the
        // shape. A pipeline that prefers one invocation can coalesce; it cannot un-union.
        Assert.EndsWith("Tests.csproj", entry.Project);
        Assert.Equal("net10.0", entry.TargetFramework);
    }

    [Fact]
    public void Invocations_is_always_an_array_and_empty_for_skip()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Unrelated() { }
            }
            """,
            changed: ["Widget.Inner"]);

        var entry = Assert.Single(ReportFixture.Build(selections).Entries);

        // A consumer iterating blindly does nothing for an empty selection, so "emit no
        // command at all" is the only representable answer rather than a rule to remember.
        Assert.Equal("skip", entry.Mode);
        Assert.Empty(entry.Invocations);
    }

    [Fact]
    public void Every_test_project_appears_even_with_an_empty_selection()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Unrelated() { }
            }
            """,
            changed: ["Widget.Inner"]);

        // So a pipeline can skip it deliberately rather than by absence.
        Assert.Single(ReportFixture.Build(selections).Entries);
    }

    [Fact]
    public void An_unknown_total_round_trips_and_is_distinguishable_from_zero()
    {
        var unknown = ReportWriter.Serialise(ReportFixture.Minimal(new ReportCounts(0, 0, null)));
        var zero = ReportWriter.Serialise(ReportFixture.Minimal(new ReportCounts(0, 0, 0)));

        // Reporting 0 for an unenumerable project would silently corrupt the over-selection
        // ratio in exactly the case where Reach is running an entire project.
        Assert.Contains("\"total\": \"unknown\"", unknown);
        Assert.Contains("\"total\": 0", zero);
        Assert.NotEqual(unknown, zero);
    }

    [Fact]
    public void The_schema_version_is_one_and_cannot_be_anything_else()
    {
        Assert.Equal(1, Report.Version);
        Assert.Contains("\"schemaVersion\": 1", ReportWriter.Serialise(ReportFixture.Minimal()));
    }

    // ---- Outcomes ---------------------------------------------------------------------------------

    [Fact]
    public void Nothing_selected_always_carries_a_non_empty_reasons_array()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Unrelated() { }
            }
            """,
            changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections);

        Assert.Equal("nothing-selected", report.Outcome);

        // Notice codes, never prose: every cause of emptiness has to be a documented,
        // register-linked code, so the taxonomy is enumerable rather than a place people add
        // ad-hoc strings.
        Assert.NotNull(report.Reasons);
        Assert.NotEmpty(report.Reasons);
        Assert.All(report.Reasons, reason => Assert.DoesNotContain(' ', reason));
    }

    [Fact]
    public void A_selected_run_carries_no_reasons()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections);

        Assert.Equal("selected", report.Outcome);
        Assert.Null(report.Reasons);
        Assert.DoesNotContain("\"reasons\"", ReportWriter.Serialise(report));
    }

    [Fact]
    public void No_changes_and_nothing_selected_are_different_outcomes()
    {
        using var nothingSelected = Selections.Of(
            Core,
            "namespace N.Tests;\n\npublic class WidgetTests { [Xunit.Fact] public void Unrelated() { } }",
            changed: ["Widget.Inner"]);

        var withChanges = ReportFixture.Build(nothingSelected);
        var withoutChanges = ReportFixture.Build(nothingSelected, noChanges: true);

        // They exit the same and mean opposite things.
        Assert.Equal("nothing-selected", withChanges.Outcome);
        Assert.Equal("no-changes", withoutChanges.Outcome);
    }

    [Fact]
    public void No_changes_still_emits_every_test_project_as_skip()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        var report = ReportFixture.Build(selections, noChanges: true);

        // So a consumer's loop behaves identically across all four outcomes; omitting entries
        // would make it a special case, and the consumer that forgets runs the full suite on
        // an empty diff.
        Assert.Equal("no-changes", report.Outcome);
        Assert.NotEmpty(report.Entries);
        Assert.All(report.Entries, entry => Assert.Empty(entry.Invocations));
    }

    [Fact]
    public void The_four_outcomes_are_a_closed_set() =>
        Assert.Equal(
            ["failed", "no-changes", "nothing-selected", "selected"],
            Enum.GetValues<Outcome>().Select(outcome => outcome.Wire()).Order(StringComparer.Ordinal));

    // ---- Sort order -------------------------------------------------------------------------------

    [Fact]
    public void Every_array_is_sorted_by_its_documented_key()
    {
        using var selections = Selections.Of(
            Core,
            Tests,
            changed: ["Widget.Spin", "Widget.Inner", "Widget.Untouched"]);

        var report = ReportFixture.Build(selections);

        Assert.Equal(
            report.Changes.Select(change => change.Display).Order(StringComparer.Ordinal),
            report.Changes.Select(change => change.Display));

        Assert.Equal(
            report.Entries.Select(entry => entry.Project).Order(StringComparer.Ordinal),
            report.Entries.Select(entry => entry.Project));

        foreach (var entry in report.Entries)
        {
            Assert.Equal(
                entry.Tests.Select(test => test.Display).Order(StringComparer.Ordinal),
                entry.Tests.Select(test => test.Display));
        }

        Assert.Equal(
            report.Notices.Select(notice => notice.Code).Order(StringComparer.Ordinal),
            report.Notices.Select(notice => notice.Code));

        Assert.Equal(
            report.Scope.Assemblies.Select(assembly => assembly.Name).Order(StringComparer.Ordinal),
            report.Scope.Assemblies.Select(assembly => assembly.Name));
    }

    [Fact]
    public void Scope_carries_identities_and_never_closure_edges()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        var scope = ReportFixture.Build(selections).Scope;

        Assert.NotEmpty(scope.TestProjects);
        Assert.All(scope.Assemblies, assembly => Assert.NotEmpty(assembly.TargetFramework));

        // Closure edges are the project graph and belong nowhere near a report.
        Assert.DoesNotContain("references", ReportWriter.Serialise(ReportFixture.Build(selections)));
    }
}
