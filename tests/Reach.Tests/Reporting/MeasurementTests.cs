using Reach.Assemblies;
using Reach.Output;
using Reach.Projects;
using Reach.Rendering;
using Reach.Reporting;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// The number that decides whether Reach is worth building further. Every input is already
/// carried for other reasons, so this is arithmetic rather than machinery — which is exactly why
/// getting the two numerators the wrong way round would be easy and invisible.
/// </summary>
public class MeasurementTests
{
    private static ProjectFile Project(string name) =>
        new(Paths.Normalise($"/repo/tests/{name}/{name}.csproj"), name, ["net10.0"], [], []);

    private static AssemblyInstance Instance(ProjectFile project) =>
        new(
            new ExpectedAssemblyInstance(project, "net10.0"),
            new ScannedAssembly(
                Paths.Normalise($"/repo/bin/{project.AssemblyName}.dll"),
                project.AssemblyName,
                Guid.NewGuid(),
                TargetFrameworkMoniker.Parse("net10.0"),
                IsFirstParty: true,
                []));

    private static ProjectSelection Selection(
        ProjectFile project,
        string? dialect,
        IReadOnlyList<string> selected,
        IReadOnlyList<string> all,
        int? total,
        SelectionMode mode = SelectionMode.Filtered,
        PathClass pathClass = PathClass.Compiled,
        int changesPerTest = 1)
    {
        var tests = all
            .Select(name => new TestMethod(
                Reach.Graph.MethodId.Definition(0, name.GetHashCode(StringComparison.Ordinal)),
                name[..name.LastIndexOf('.')],
                name[(name.LastIndexOf('.') + 1)..]))
            .ToArray();

        return new ProjectSelection(
            project,
            "net10.0",
            mode,
            [
                .. tests
                    .Where(test => selected.Contains(test.FullyQualifiedName))
                    .Select(test => new SelectedTest(
                        test,
                        [SelectionRule.ReverseReachable],
                        [.. Enumerable.Range(0, changesPerTest)],
                        pathClass))
            ],
            total,
            dialect,
            tests);
    }

    private static ReportMeasurement Measure(params ProjectSelection[] projects)
    {
        using var directory = TempDirectory.Create("reach-measure");

        var rendered = new Renderer(
                [.. projects.Select(project => Instance(project.Project))],
                new SelectRequest { WorkingDirectory = directory.Path },
                ReachDirectory.For(directory.Path, null))
            .Render(new SelectionResult(projects, [], []));

        return ReportMeasurement.Of(rendered);
    }

    // ---- The numerator ----------------------------------------------------------------------

    [Fact]
    public void The_numerator_is_what_will_run_not_what_was_selected()
    {
        var measurement = Measure(Selection(
            Project("Suite"),
            "nunit",
            ["N.C.MyTest"],
            ["N.C.MyTest", "N.C.MyTest2", "N.C.Other"],
            total: 3));

        // A dialect-level over-match *is* over-selection, so counting the canonical selection
        // would flatter the ratio by exactly the amount one named decision costs. Both numbers
        // are carried, and the gap between them is the price.
        Assert.Equal(1, measurement.Selected);
        Assert.Equal(2, measurement.WillRun);
        Assert.Equal(3, measurement.Total);
        // Rounded, so the report is byte-deterministic rather than carrying a float's full tail.
        Assert.Equal(0.6667, measurement.Ratio);
    }

    [Fact]
    public void An_equality_dialect_has_no_gap()
    {
        var measurement = Measure(Selection(
            Project("Suite"),
            "xunit-v3-4",
            ["N.C.MyTest"],
            ["N.C.MyTest", "N.C.MyTest2"],
            total: 2));

        Assert.Equal(measurement.Selected, measurement.WillRun);
    }

    // ---- The denominator ----------------------------------------------------------------------

    [Fact]
    public void An_unenumerable_project_contributes_to_neither_side()
    {
        var measurement = Measure(
            Selection(Project("Known"), "xunit-v3-4", ["N.C.A"], ["N.C.A", "N.C.B"], total: 2),
            Selection(Project("Unknown"), null, [], ["N.D.A"], total: null, mode: SelectionMode.RunAll));

        // Treating an unenumerable project as zero would flatter the ratio in exactly the case
        // where Reach is running an entire project — the one case where the number most needs
        // to be ugly.
        Assert.Equal(2, measurement.Total);
        Assert.Equal(1, measurement.WillRun);
        Assert.Equal(1, measurement.ProjectsRunInFull);
    }

    [Fact]
    public void The_summary_line_carries_both_numbers()
    {
        var measurement = Measure(
            Selection(Project("Known"), "xunit-v3-4", ["N.C.A"], ["N.C.A", "N.C.B"], total: 2),
            Selection(Project("Unknown"), null, [], ["N.D.A"], total: null, mode: SelectionMode.RunAll));

        Assert.Equal(
            "selected 1 of 2 tests (50%) · 1 project(s) on unrecognised frameworks run in full",
            measurement.Describe());
    }

    [Fact]
    public void Nothing_enumerable_reports_an_unknown_ratio()
    {
        var measurement = Measure(
            Selection(Project("Unknown"), null, [], ["N.D.A"], total: null, mode: SelectionMode.RunAll));

        Assert.Null(measurement.Ratio);
        Assert.Contains("unknown total", measurement.Describe());
    }

    // ---- The widening delta ---------------------------------------------------------------------

    [Fact]
    public void The_widening_delta_counts_pairs_whose_path_class_is_widened()
    {
        var measurement = Measure(Selection(
            Project("Suite"),
            "xunit-v3-4",
            ["N.C.A"],
            ["N.C.A", "N.C.B"],
            total: 2,
            pathClass: PathClass.Widened,
            changesPerTest: 3));

        // Pairs, not tests: one change reaching a test only through widening is the unit.
        Assert.Equal(3, measurement.WidenedPairs);
    }

    [Fact]
    public void A_test_reachable_both_ways_is_not_counted()
    {
        var measurement = Measure(Selection(
            Project("Suite"),
            "xunit-v3-4",
            ["N.C.A"],
            ["N.C.A"],
            total: 1,
            pathClass: PathClass.Compiled,
            changesPerTest: 3));

        // A test with both a compiled and a widened path reads compiled, so the cheap number
        // *understates* widening's contribution — a registered measurement limitation, with a
        // second traversal as the upgrade path.
        Assert.Equal(0, measurement.WidenedPairs);
    }
}
