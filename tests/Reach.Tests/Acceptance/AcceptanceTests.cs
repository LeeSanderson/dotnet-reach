using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Acceptance;

/// <summary>
/// M1's acceptance gate, over a real solution that a real MSBuild built.
/// </summary>
/// <remarks>
/// <para>
/// Each of these earns an end-to-end slot by the same criterion: <strong>it fails silently if it
/// regresses</strong> — no exception, no crash, just a wrong answer that looks plausible.
/// Everything that fails loudly, and every IL shape, is tested in memory instead.
/// </para>
/// <para>
/// Filterable as a group with <c>--filter Reach.Tests.Acceptance</c>, so the fast suite stays
/// fast in the single test project.
/// </para>
/// </remarks>
public sealed class AcceptanceTests : IDisposable
{
    private readonly SampleCopy _fixture;
    private readonly string _baseline;

    public AcceptanceTests() => _fixture = SampleCopy.Build(out _baseline);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ReportEntry EntryFor(Report report, string project) =>
        Assert.Single(report.Entries, entry => entry.Project.Contains(project, StringComparison.Ordinal));

    // ---- 1. The NUnit `~` filter, in both directions -------------------------------------------

    [Fact]
    public async Task The_NUnit_filter_renders_with_contains_and_reports_the_extra_it_will_run()
    {
        Touch("src/Contoso.Core/Invoice.cs", "public static int Tax(int net) => net * StandardRate / Percent;",
            "public static int Tax(int net) => (net * StandardRate / Percent) + 0;");

        var report = await _fixture.SelectAsync(_baseline, Token);
        var entry = EntryFor(report, "Contoso.Tests.NUnit");

        // The parameterised test survives the rendered filter, which is the whole reason the
        // dialect renders with ~ rather than equality: NUnit's VSTest FullyQualifiedName
        // includes a parameterised test's arguments.
        Assert.Contains(entry.Tests, test => test.Display.EndsWith(".MyTest", StringComparison.Ordinal));

        var filter = Assert.Single(entry.Invocations)
            .SkipWhile(argument => argument != "--filter")
            .Skip(1)
            .Single();

        Assert.Contains("FullyQualifiedName~", filter);
        Assert.DoesNotContain("FullyQualifiedName=", filter);

        // And the second number: MyTest2 is matched by a `~` filter naming MyTest, so what runs
        // is a strict superset of what was selected. The headline metric reads this, and a
        // fixture that let it silently return zero would corrupt the measurement.
        Assert.True(
            entry.Counts.WillRun > entry.Counts.Selected,
            $"expected the rendered filter to over-match: selected {entry.Counts.Selected}, "
            + $"will run {entry.Counts.WillRun}");

        Assert.Contains(report.Notices, notice => notice.Code == NoticeCodes.DialectOverSelects);
        Assert.Contains(report.Notices, notice => notice.Code == NoticeCodes.NUnitFilterOvermatch);
    }

    // ---- 3. Whole-project fallback ------------------------------------------------------------------

    [Fact]
    public async Task An_unrecognised_framework_runs_in_full_with_an_unknown_total()
    {
        Touch("src/Contoso.Core/Invoice.cs", "=> net + Tax(net);", "=> net + Tax(net) + 0;");

        var report = await _fixture.SelectAsync(_baseline, Token);
        var entry = EntryFor(report, "Contoso.Tests.Unrecognised");

        Assert.Equal("run-all", entry.Mode);

        // unknown, never 0: reporting zero would silently corrupt the ratio in exactly the case
        // where Reach is running an entire project.
        Assert.Null(entry.Counts.Total);
        Assert.Contains("\"total\": \"unknown\"", ReportWriter.Serialise(report));

        Assert.Contains(report.Notices, notice => notice.Code == NoticeCodes.WholeProjectFallback);
    }

    // ---- Determinism and the exact selection now live in Basics/BasicsTests and Expect ------------

    [Fact]
    public async Task A_multi_targeted_project_contributes_an_entry_per_framework()
    {
        Touch("src/Contoso.Core/Invoice.cs", "=> net + Tax(net);", "=> net + Tax(net) + 0;");

        var report = await _fixture.SelectAsync(_baseline, Token);

        // Contoso.Core is net8.0;net10.0, so discovery resolved two assembly instances for it.
        Assert.Equal(
            ["net10.0", "net8.0"],
            report.Scope.Assemblies
                .Where(assembly => assembly.Name == "Contoso.Core")
                .Select(assembly => assembly.TargetFramework)
                .Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_changed_const_consumed_across_an_assembly_boundary_selects_the_consumers_tests()
    {
        Touch("src/Contoso.Core/Invoice.cs", "StandardRate = 20;", "StandardRate = 25;");

        var report = await _fixture.SelectAsync(_baseline, Token);

        // After inlining the consumer's source is byte-identical and its IL is different, so
        // nothing on the declaring side reaches this.
        Assert.NotEmpty(EntryFor(report, "Contoso.Tests.Xunit").Tests);
    }

    [Fact]
    public async Task Fact_added_to_an_existing_method_selects_it()
    {
        Touch(
            "tests/Contoso.Tests.Xunit/InvoiceTests.cs",
            "    [Fact]\n    public void Untouched_is_zero()",
            "    [Fact]\n    [Trait(\"kind\", \"new\")]\n    public void Untouched_is_zero()");

        var report = await _fixture.SelectAsync(_baseline, Token);

        // The cheapest test of the most expensive regression in the set.
        Assert.Contains(
            EntryFor(report, "Contoso.Tests.Xunit").Tests,
            test => test.Display.EndsWith(".Untouched_is_zero", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_project_on_a_framework_needing_the_runner_setting_says_so()
    {
        Touch("src/Contoso.Core/Invoice.cs", "=> net + Tax(net);", "=> net + Tax(net) + 0;");

        var report = await _fixture.SelectAsync(_baseline, Token);

        // The fixture has no global.json, and its xUnit project is on xunit.v3 4.0.0 — where
        // `dotnet test` is a hard build error whose message names nothing Reach-shaped. Reach
        // still emits the invocations: the rendering is correct, the repository is not.
        var notice = Assert.Single(report.Notices, n => n.Code == NoticeCodes.TestRunnerNotConfigured);

        Assert.Contains("Microsoft.Testing.Platform", notice.Message);
        Assert.NotEmpty(EntryFor(report, "Contoso.Tests.Xunit").Invocations);
    }

    /// <summary>
    /// Applies a change and rebuilds. The rebuild is not optional: editing source under
    /// --no-build is a correspondence failure by design, which is a different assertion from
    /// every one in this file.
    /// </summary>
    /// <remarks>
    /// <paramref name="find"/> and <paramref name="replace"/> are written with <c>\n</c>; they are
    /// translated to the line ending the fixture file was checked out with, so the edit matches
    /// on a CRLF checkout as well as an LF one.
    /// </remarks>
    private void Touch(string relativePath, string find, string replace)
    {
        var contents = _fixture.ReadFile(relativePath);
        var newline = contents.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

        var matched = find.ReplaceLineEndings(newline);

        Assert.Contains(matched, contents);

        _fixture.WriteFile(
            relativePath,
            contents.Replace(matched, replace.ReplaceLineEndings(newline), StringComparison.Ordinal));
        _fixture.Rebuild();
    }

    public void Dispose() => _fixture.Dispose();
}
