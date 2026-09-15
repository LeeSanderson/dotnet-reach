using Reach.Assemblies;
using Reach.Output;
using Reach.Projects;
using Reach.Rendering;
using Reach.Reporting;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Rendering;

/// <summary>
/// The argument vectors themselves, over synthesised selections so that every shape — one
/// framework, several, a platform suffix, a long selection, a runsettings file — is one test.
/// </summary>
public class InvocationTests
{
    private static ProjectFile Project(string name = "Tests") =>
        new(Paths.Normalise($"/repo/tests/{name}/{name}.csproj"), name, ["net10.0"], [], []);

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
        string dialect,
        IReadOnlyList<string> selected,
        IReadOnlyList<string>? all = null,
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
            tests.Length,
            dialect,
            tests);
    }

    private static RenderedSelection Render(
        IReadOnlyList<AssemblyInstance> instances,
        IReadOnlyList<ProjectSelection> projects,
        TempDirectory directory,
        SelectRequest? request = null) =>
        new Renderer(
                instances,
                (request ?? new SelectRequest()) with { WorkingDirectory = directory.Path },
                ReachDirectory.For(directory.Path, null))
            .Render(new SelectionResult(projects, [], []));

    // ---- Golden argv, one per row of the recognition table -------------------------------------

    [Theory]
    [InlineData("xunit-v2")]
    [InlineData("xunit-v3")]
    [InlineData("xunit-v3-4")]
    [InlineData("mstest")]
    public void The_argv_for_an_equality_dialect(string dialect)
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();
        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", dialect, ["N.Tests.C.M"])],
            directory);

        Assert.Equal(
            ["dotnet", "test", project.Path, "--filter", "FullyQualifiedName=N.Tests.C.M"],
            Assert.Single(Assert.Single(rendered.Entries).Invocations));
    }

    [Fact]
    public void The_argv_for_NUnit_uses_contains()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();
        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "nunit", ["N.Tests.C.M"])],
            directory);

        Assert.Equal(
            ["dotnet", "test", project.Path, "--filter", "FullyQualifiedName~N.Tests.C.M"],
            Assert.Single(Assert.Single(rendered.Entries).Invocations));
    }

    [Fact]
    public void No_emitted_argv_ever_contains_dash_o()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"])],
            directory,
            new SelectRequest { Output = "out", Configuration = "Release" });

        // MTP-mode `dotnet test` has no -o at all and spells --output to mean test output
        // *verbosity*, while VSTest-mode `dotnet test` and `dotnet build` use it for a
        // directory.
        foreach (var invocation in rendered.Entries.SelectMany(entry => entry.Invocations))
        {
            Assert.DoesNotContain("-o", invocation);
            Assert.DoesNotContain("--output", invocation);
        }
    }

    // ---- Rule 2: the framework selector ----------------------------------------------------------

    [Fact]
    public void A_single_instance_project_gets_no_framework_selector()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();
        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"])],
            directory);

        // A single-targeted net10.0-windows suite never needs a moniker and never degrades.
        Assert.DoesNotContain("-f", Assert.Single(Assert.Single(rendered.Entries).Invocations));
    }

    [Fact]
    public void Every_invocation_for_a_multi_instance_project_carries_a_matching_selector()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();

        var rendered = Render(
            [
                Instance(project, "net8.0", ".NETCoreApp,Version=v8.0"),
                Instance(project, "net10.0"),
            ],
            [
                Selection(project, "net8.0", "xunit-v3-4", ["N.Tests.C.M"]),
                Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"]),
            ],
            directory);

        // `dotnet test` runs every target framework of a project, so a filter naming a test
        // that exists under only one makes the others exit 8 — Reach would render the *correct*
        // answer and fail the build with it.
        foreach (var entry in rendered.Entries)
        {
            var invocation = Assert.Single(entry.Invocations);
            var index = invocation.ToList().IndexOf("-f");

            Assert.True(index >= 0, $"no selector on the {entry.Selection.TargetFramework} entry");
            Assert.Equal(entry.Selection.TargetFramework, invocation[index + 1]);
        }
    }

    [Fact]
    public void A_multi_instance_project_with_a_platform_suffix_runs_project_wide_once()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();

        var rendered = Render(
            [
                Instance(project, "net10.0"),
                Instance(project, "net10.0-windows", platform: "Windows7.0"),
            ],
            [
                Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"]),
                Selection(project, "net10.0-windows", "xunit-v3-4", ["N.Tests.C.M"]),
            ],
            directory);

        // The single project-wide invocation is carried by the first entry in the report's own
        // sort order and the rest carry empty arrays, so a consumer's loop issues exactly one
        // command — and `mode` is the field that says there is something to run.
        Assert.All(rendered.Entries, entry => Assert.Equal(SelectionMode.RunAll, entry.Mode));
        Assert.Single(rendered.Entries.SelectMany(entry => entry.Invocations));
        Assert.Single(rendered.Entries, entry => entry.Invocations.Count == 1);

        var invocation = rendered.Entries.SelectMany(entry => entry.Invocations).Single();

        Assert.DoesNotContain("-f", invocation);
        Assert.DoesNotContain("--filter", invocation);

        Assert.Contains(
            rendered.Notices,
            notice => notice.Code == NoticeCodes.FrameworkSelectorUnderivable);
    }

    // ---- Delivery ----------------------------------------------------------------------------------

    [Fact]
    public void A_long_selection_on_the_testing_platform_goes_through_a_response_file()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();
        var names = LongSelection();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", names)],
            directory);

        var entry = Assert.Single(rendered.Entries);
        var invocation = Assert.Single(entry.Invocations);
        var responseFile = Assert.Single(invocation, argument => argument.StartsWith('@'));

        Assert.Equal(Delivery.ResponseFile, entry.Delivery);

        // Absolute, so a pipeline that changes directory between steps still works.
        Assert.True(Path.IsPathRooted(responseFile[1..]));
        Assert.True(File.Exists(responseFile[1..]));

        // In the directory Reach owns — never a path MSBuild or Visual Studio reads by
        // convention, and never the system temp directory.
        Assert.StartsWith(ReachDirectory.For(directory.Path, null).Path, responseFile[1..]);
    }

    [Fact]
    public void A_long_selection_on_a_host_with_no_channel_is_chunked()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();
        var names = LongSelection();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "nunit", names)],
            directory);

        var entry = Assert.Single(rendered.Entries);

        Assert.Equal(Delivery.Chunked, entry.Delivery);
        Assert.True(entry.Invocations.Count > 1);

        // Every chunk carries at least one test by construction, which is half of why no path
        // through Reach's design renders a filter matching nothing.
        Assert.All(entry.Invocations, invocation => Assert.Contains("--filter", invocation));
        Assert.All(
            entry.Invocations,
            invocation => Assert.NotEmpty(invocation[(invocation.ToList().IndexOf("--filter") + 1)]));
    }

    [Fact]
    public void Re_running_overwrites_a_side_car_rather_than_accumulating()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();
        var names = LongSelection();

        Render([Instance(project, "net10.0")], [Selection(project, "net10.0", "xunit-v3-4", names)], directory);
        Render([Instance(project, "net10.0")], [Selection(project, "net10.0", "xunit-v3-4", names)], directory);

        var files = Directory.GetFiles(ReachDirectory.For(directory.Path, null).Path, "*.rsp");

        Assert.Single(files);
    }

    // ---- Empty selections ---------------------------------------------------------------------------

    [Fact]
    public void An_empty_selection_emits_no_command_at_all()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", [], ["N.Tests.C.M"], SelectionMode.Skip)],
            directory);

        // Not an empty filter string, which runs everything.
        Assert.Empty(Assert.Single(rendered.Entries).Invocations);
    }

    [Fact]
    public void Run_all_emits_one_invocation_with_no_filter()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", [], ["N.Tests.C.M"], SelectionMode.RunAll)],
            directory);

        Assert.DoesNotContain("--filter", Assert.Single(Assert.Single(rendered.Entries).Invocations));
    }

    // ---- Runsettings -----------------------------------------------------------------------------------

    [Fact]
    public void A_runsettings_file_without_a_filter_is_merged()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var settings = Path.Combine(directory.Path, "my.runsettings");
        File.WriteAllText(settings, "<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration></RunSettings>");

        var project = Project();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"])],
            directory,
            new SelectRequest { RunSettings = settings });

        var entry = Assert.Single(rendered.Entries);
        var invocation = Assert.Single(entry.Invocations);
        var merged = invocation[invocation.ToList().IndexOf("--settings") + 1];

        Assert.Equal(Delivery.RunSettings, entry.Delivery);
        Assert.Contains("TestCaseFilter", File.ReadAllText(merged));
        Assert.Contains("MaxCpuCount", File.ReadAllText(merged));
    }

    [Fact]
    public void A_runsettings_file_that_already_filters_downgrades_to_run_all()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var settings = Path.Combine(directory.Path, "my.runsettings");
        File.WriteAllText(
            settings,
            "<RunSettings><RunConfiguration><TestCaseFilter>Category!=Slow</TestCaseFilter></RunConfiguration></RunSettings>");

        var project = Project();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.M"])],
            directory,
            new SelectRequest { RunSettings = settings });

        var entry = Assert.Single(rendered.Entries);

        // A pre-existing filter is AND-ed with Reach's, and a consumer's filter is typically an
        // exclusion — so the intersection would be strictly smaller than the selection, and
        // undetectably so.
        Assert.Equal(SelectionMode.RunAll, entry.Mode);
        Assert.DoesNotContain("--filter", Assert.Single(entry.Invocations));
        Assert.Contains(rendered.Notices, notice => notice.Code == NoticeCodes.RunSettingsFilterConflict);
    }

    // ---- Both counts ---------------------------------------------------------------------------------------

    [Fact]
    public void A_contains_dialect_reports_the_extra_it_will_run()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "nunit", ["N.Tests.C.MyTest"], ["N.Tests.C.MyTest", "N.Tests.C.MyTest2"])],
            directory);

        var entry = Assert.Single(rendered.Entries);

        Assert.Single(entry.Selection.Selected);
        Assert.Equal(2, entry.WillRun);

        Assert.Contains(rendered.Notices, notice => notice.Code == NoticeCodes.DialectOverSelects);
    }

    [Fact]
    public void An_equality_dialect_reports_no_extra()
    {
        using var directory = TempDirectory.Create("reach-argv");

        var project = Project();

        var rendered = Render(
            [Instance(project, "net10.0")],
            [Selection(project, "net10.0", "xunit-v3-4", ["N.Tests.C.MyTest"], ["N.Tests.C.MyTest", "N.Tests.C.MyTest2"])],
            directory);

        Assert.Equal(1, Assert.Single(rendered.Entries).WillRun);
        Assert.DoesNotContain(rendered.Notices, notice => notice.Code == NoticeCodes.DialectOverSelects);
    }

    private static IReadOnlyList<string> LongSelection() =>
    [
        .. Enumerable.Range(0, 300)
            .Select(index => $"Contoso.Billing.Tests.InvoiceTests.Calculates_the_total_case_{index:D3}")
    ];
}
