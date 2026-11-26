using System.Text.Json;
using Reach.Assemblies;
using Reach.Output;
using Reach.Projects;
using Reach.Rendering;
using Reach.Reporting;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Reporting;

/// <summary>
/// The fields of one report entry that say how its tests reach a runner: <c>runnerHost</c>,
/// <c>delivery</c> and <c>counts.willRun</c>. Asserted on the serialised JSON, because "absent"
/// is a claim about the document a consumer reads, not about a null in memory.
/// </summary>
public class ReportEntryTests
{
    // ---- runnerHost --------------------------------------------------------------------------------

    [Fact]
    public void A_filtered_entry_names_dotnet_test_as_its_runner_host()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var project = Project();
        var entry = Assert.Single(Entries(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"])],
            directory));

        Assert.Equal("dotnet-test", entry.GetProperty("runnerHost").GetString());
    }

    [Fact]
    public void A_skip_entry_carries_no_runner_host()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var project = Project();
        var entry = Assert.Single(Entries(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", [], ["N.Tests.C.M"], total: 1, SelectionMode.Skip)],
            directory));

        Assert.Equal("skip", entry.GetProperty("mode").GetString());
        Assert.False(entry.TryGetProperty("runnerHost", out _));
    }

    [Fact]
    public void A_project_run_in_full_still_names_its_runner_host()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var project = Project();
        var entry = Assert.Single(Entries(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", null, [], ["N.Tests.C.M"], total: null, SelectionMode.RunAll)],
            directory));

        // An unrecognised framework: no dialect, but the project still runs under dotnet test.
        Assert.Equal("run-all", entry.GetProperty("mode").GetString());
        Assert.Equal("dotnet-test", entry.GetProperty("runnerHost").GetString());
    }

    // ---- delivery ----------------------------------------------------------------------------------

    [Fact]
    public void A_short_selection_is_delivered_inline()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var project = Project();
        var entry = Assert.Single(Entries(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"])],
            directory));

        Assert.Equal("inline", entry.GetProperty("delivery").GetString());
    }

    [Fact]
    public void A_selection_merged_into_a_runsettings_file_is_delivered_by_run_settings()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var settings = Path.Combine(directory.Path, "my.runsettings");
        File.WriteAllText(settings, "<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration></RunSettings>");

        var project = Project();
        var entry = Assert.Single(Entries(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"])],
            directory,
            new SelectRequest { RunSettings = settings }));

        Assert.Equal("run-settings", entry.GetProperty("delivery").GetString());
    }

    [Fact]
    public void A_skip_entry_carries_no_delivery()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var project = Project();
        var entry = Assert.Single(Entries(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", [], ["N.Tests.C.M"], total: 1, SelectionMode.Skip)],
            directory));

        Assert.False(entry.TryGetProperty("delivery", out _));
    }

    [Fact]
    public void The_empty_entries_of_an_underivable_moniker_carry_no_delivery()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var project = Project();
        var entries = Entries(
            [
                Instance(project, "net10.0"),
                Instance(project, "net10.0-windows", platform: "Windows7.0"),
            ],
            [
                Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"]),
                Selection(project, "net10.0-windows", "xunit-v3-4", ["N.Tests.C.M"]),
            ],
            directory);

        // One entry carries the single project-wide command and says how it is delivered; the
        // other delivers nothing, so it has no delivery to name.
        var carrying = Assert.Single(entries, entry => entry.GetProperty("invocations").GetArrayLength() == 1);
        var empty = Assert.Single(entries, entry => entry.GetProperty("invocations").GetArrayLength() == 0);

        Assert.Equal("inline", carrying.GetProperty("delivery").GetString());
        Assert.Equal("run-all", empty.GetProperty("mode").GetString());
        Assert.False(empty.TryGetProperty("delivery", out _));
    }

    // ---- counts.willRun ----------------------------------------------------------------------------

    [Fact]
    public void An_entry_whose_total_is_unknown_will_run_zero()
    {
        using var directory = TempDirectory.Create("reach-entry");

        var project = Project();
        var entry = Assert.Single(Entries(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", null, [], ["N.Tests.C.A", "N.Tests.C.B"], total: null, SelectionMode.RunAll)],
            directory));

        var counts = entry.GetProperty("counts");

        // Every test in the project runs, but the count is unknown and inventing one would be
        // worse: willRun is what Reach can account for.
        Assert.Equal("unknown", counts.GetProperty("total").GetString());
        Assert.Equal(0, counts.GetProperty("willRun").GetInt32());
    }

    // ---- Helpers -----------------------------------------------------------------------------------

    private static ProjectFile Project() =>
        new(Paths.Normalise("/repo/tests/Tests/Tests.csproj"), "Tests", ["net10.0"], [], []);

    private static AssemblyInstance Instance(
        ProjectFile project,
        string targetFramework,
        string frameworkName = ".NETCoreApp,Version=v10.0",
        string? platform = null) =>
        new(
            new ExpectedAssemblyInstance(project, targetFramework),
            new ScannedAssembly(
                Paths.Normalise($"/repo/bin/{targetFramework}/{project.AssemblyName}.dll"),
                project.AssemblyName,
                Guid.NewGuid(),
                TargetFrameworkMoniker.FromAttributes(frameworkName, platform),
                IsFirstParty: true,
                []));

    private static ProjectSelection Selection(
        ProjectFile project,
        string targetFramework,
        string? dialect,
        IReadOnlyList<string> selected,
        IReadOnlyList<string>? all = null,
        int? total = 1,
        SelectionMode mode = SelectionMode.Filtered)
    {
        var tests = (all ?? selected)
            .Select(name => new TestMethod(
                Reach.Graph.MethodId.Definition(0, name.GetHashCode(StringComparison.Ordinal)),
                name[..name.LastIndexOf('.')],
                name[(name.LastIndexOf('.') + 1)..]))
            .ToArray();

        return new ProjectSelection(
            project,
            targetFramework,
            mode,
            [
                .. tests
                    .Where(test => selected.Contains(test.FullyQualifiedName))
                    .Select(test => new SelectedTest(test, [SelectionRule.ReverseReachable], [0], PathClass.Compiled))
            ],
            total,
            dialect,
            tests);
    }

    /// <summary>
    /// Renders the projects, builds the report from the rendering, and returns the serialised
    /// report's entries.
    /// </summary>
    private static IReadOnlyList<JsonElement> Entries(
        IReadOnlyList<AssemblyInstance> instances,
        IReadOnlyList<ProjectSelection> projects,
        TempDirectory directory,
        SelectRequest? request = null)
    {
        var actual = (request ?? new SelectRequest()) with { WorkingDirectory = directory.Path };
        var selection = new SelectionResult(projects, [], []);

        var rendered = new Renderer(instances, actual, ReachDirectory.For(directory.Path, null))
            .Render(selection);

        var run = new SelectRun(
            ExitCode.Success,
            string.Empty,
            rendered.Notices,
            Assemblies: instances,
            Selection: selection,
            Rendered: rendered);

        using var document = JsonDocument.Parse(
            ReportWriter.Serialise(ReportBuilder.Build(run, actual, new PhaseTimings())));

        return [.. document.RootElement.GetProperty("entries").EnumerateArray().Select(entry => entry.Clone())];
    }
}
