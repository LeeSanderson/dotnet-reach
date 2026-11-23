using Reach.Processes;
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

    // ---- 2. An empty selection -------------------------------------------------------------------

    [Fact]
    public async Task An_empty_selection_is_skip_with_no_invocations_and_starts_no_process()
    {
        // A comment-only change: real paths in the diff, no changed member anywhere.
        Touch(
            "src/Contoso.Core/Invoice.cs",
            "public static int Untouched(int net) => net;",
            "// nothing here changes what compiles\n    public static int Untouched(int net) => net;");

        var report = await _fixture.SelectAsync(_baseline, Token);

        // Every project Reach could analyse. The unrecognised-framework project is not among
        // them and runs in full whatever the change was, because Reach cannot show it is
        // unaffected — which is the safe direction and a different assertion.
        var analysable = report.Entries.Where(entry => entry.Counts.Total is not null).ToArray();

        Assert.NotEmpty(analysable);
        Assert.All(analysable, entry => Assert.Equal("skip", entry.Mode));
        Assert.All(analysable, entry => Assert.Empty(entry.Invocations));

        // The consumer's half, which is what earns the end-to-end slot. Two opposite failure
        // modes are closed by the same fact: an empty filter string runs everything, and under
        // Microsoft.Testing.Platform an empty-match filter exits 8 and turns a green build red.
        var runner = new ScriptedProcessRunner();
        var issued = 0;

        foreach (var invocation in analysable.SelectMany(entry => entry.Invocations))
        {
            issued++;

            await runner.RunAsync(
                new ProcessRequest(invocation[0], [.. invocation.Skip(1)], _fixture.Path),
                Token);
        }

        Assert.Equal(0, issued);
        Assert.Empty(runner.Requests);
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
    public async Task The_generator_project_is_in_scope_and_not_expected_on_disk()
    {
        Touch("src/Contoso.Core/Invoice.cs", "=> net + Tax(net);", "=> net + Tax(net) + 0;");

        var report = await _fixture.SelectAsync(_baseline, Token);

        // Referenced with ReferenceOutputAssembly="false", so it produces neither a metadata
        // reference nor a copy — in the closure, and not in the expected assembly set. A wrong
        // exclusion here would be a false exit 3.
        Assert.DoesNotContain(report.Scope.Assemblies, assembly => assembly.Name == "Contoso.Generator");
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
    public async Task A_change_reaching_no_test_appears_with_a_count_of_zero()
    {
        // Untouched() is called by exactly one xUnit test and nothing else, so changing the
        // generator's marker reaches nothing at all.
        Touch("src/Contoso.Generator/Marker.cs", "\"contoso\"", "\"contoso-2\"");

        var report = await _fixture.SelectAsync(_baseline, Token);

        // The field that makes an under-selection visible, and only trustworthy if something
        // proves it fires.
        Assert.Contains(report.Changes, change => change.TestsReached == 0);
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
