using Reach.Changes;
using Reach.Projects;
using Reach.Reporting;

namespace Reach.Tests.Changes;

/// <summary>
/// The rule table. Directory containment is the load-bearing idea, and the negative test for it
/// is what proves the table has not degenerated into "select everything".
/// </summary>
public class TierLadderTests
{
    private const string Root = "/repo";

    /// <summary>
    /// Two services, each with its own project, plus a shared library — enough shape for
    /// containment to be able to get it wrong.
    /// </summary>
    private static AnalysisScope Scope()
    {
        ProjectFile Project(string path, string name, params ProjectReference[] references) =>
            new(Paths.Normalise(path), name, ["net10.0"], references, []);

        var generator = Project("/repo/src/Generator/Generator.csproj", "Generator");

        var billing = Project(
            "/repo/services/billing/Billing/Billing.csproj",
            "Billing",
            new ProjectReference(generator.Path, ReferenceOutputAssembly: false, IsAnalyzer: true));

        var shipping = Project("/repo/services/shipping/Shipping/Shipping.csproj", "Shipping");
        var shared = Project("/repo/src/Shared/Shared.csproj", "Shared");

        return new AnalysisScope([generator, billing, shipping, shared], [billing], []);
    }

    private static RoutedChange Route(string path) =>
        new TierLadder(Scope(), Root).Route(new ChangedPath(path, ChangeStatus.Modified));

    private static string[] Names(RoutedChange routed) =>
        [.. routed.Projects.Select(project => project.Name).Order(StringComparer.Ordinal)];

    // ---- Row 1 --------------------------------------------------------------------------------

    [Fact]
    public void A_project_file_selects_only_its_own_project() =>
        Assert.Equal(["Shipping"], Names(Route("services/shipping/Shipping/Shipping.csproj")));

    // ---- Row 2, and the assertion that proves containment is real --------------------------------

    [Fact]
    public void A_Directory_Build_props_under_a_subdirectory_does_not_select_projects_outside_it()
    {
        var routed = Route("services/billing/Directory.Build.props");

        // MSBuild's own props discovery walks *up* from each project, so containment is not a
        // heuristic — it is the same rule the build itself uses. Without it, one service's
        // build file would widen the whole solution.
        Assert.Equal(["Billing"], Names(routed));
        Assert.DoesNotContain("Shipping", Names(routed));
        Assert.DoesNotContain("Shared", Names(routed));
    }

    [Fact]
    public void A_Directory_Build_props_at_the_root_selects_every_project() =>
        Assert.Equal(
            ["Billing", "Generator", "Shared", "Shipping"],
            Names(Route("Directory.Build.props")));

    [Theory]
    [InlineData("services/billing/Directory.Build.props")]
    [InlineData("services/billing/Directory.Build.targets")]
    [InlineData("services/billing/.editorconfig")]
    public void Each_directory_scoped_build_file_is_bounded_by_its_directory(string path) =>
        Assert.Equal(["Billing"], Names(Route(path)));

    // ---- Row 3 -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("global.json")]
    [InlineData("nuget.config")]
    [InlineData("packages.lock.json")]
    [InlineData("Directory.Packages.props")]
    [InlineData("Solution.slnx")]
    [InlineData("Solution.sln")]
    public void A_solution_wide_file_selects_every_in_scope_project(string path) =>
        Assert.Equal(
            ["Billing", "Generator", "Shared", "Shipping"],
            Names(Route(path)));

    [Fact]
    public void Only_row_three_is_solution_wide()
    {
        // The table degenerates the moment a second row becomes solution-wide, so this is worth
        // asserting as a property rather than only per row.
        var solutionWide = new[]
        {
            "global.json",
            "nuget.config",
            "packages.lock.json",
            "Directory.Packages.props",
            "Solution.slnx",
        };

        var everythingElse = new[]
        {
            "services/billing/Directory.Build.props",
            "services/billing/Billing/Billing.csproj",
            "services/billing/Billing/appsettings.json",
            "README.md",
        };

        Assert.All(solutionWide, path => Assert.Equal(4, Route(path).Projects.Count));
        Assert.All(everythingElse, path => Assert.True(Route(path).Projects.Count < 4, path));
    }

    // ---- Row 4 -----------------------------------------------------------------------------------

    [Fact]
    public void A_generator_project_selects_every_project_that_consumes_it()
    {
        // Detected by the conventional marker — a ProjectReference carrying
        // OutputItemType="Analyzer". The generator's own output lands nowhere Reach scans, so
        // what widens is its consumers.
        Assert.Equal(["Billing"], Names(Route("src/Generator/Generator.csproj")));
    }

    [Fact]
    public void A_project_nothing_references_as_an_analyser_is_an_ordinary_project() =>
        Assert.Equal(["Shared"], Names(Route("src/Shared/Shared.csproj")));

    // ---- Row 5 -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("services/billing/Billing/appsettings.json")]
    [InlineData("services/billing/Billing/Strings.resx")]
    [InlineData("services/billing/Billing/some.config")]
    public void Content_copied_to_the_output_selects_its_containing_project(string path) =>
        Assert.Equal(["Billing"], Names(Route(path)));

    // ---- Row 6 -----------------------------------------------------------------------------------

    [Fact]
    public void An_unrecognised_file_with_no_ancestor_project_selects_nothing()
    {
        var routed = Route("README.md");

        // The only rule in Reach that errs toward selecting nothing. The alternative —
        // whole-solution selection for any unrecognised file — means a README change runs the
        // entire suite, which is not conservatism with a cost but a tool nobody keeps switched
        // on.
        Assert.Empty(routed.Projects);
        Assert.True(routed.Rule.ErrsUnder);
    }

    [Fact]
    public void That_case_emits_its_own_notice_and_names_the_file()
    {
        var notice = TierLadder.UnattributedNotice(
            [Route("README.md"), Route(".gitignore"), Route("services/billing/Billing/Billing.csproj")]);

        // Precise rather than a blanket disclaimer: it fires only when row 6 matched with no
        // ancestor project.
        Assert.NotNull(notice);
        Assert.Equal(NoticeCodes.UnmappedFileNoProject, notice.Code);
        Assert.Equal(NoticeKind.BlindSpot, notice.Kind);
        Assert.Contains("README.md", notice.Message);
        Assert.DoesNotContain("Billing.csproj", notice.Message);
    }

    [Fact]
    public void A_run_with_nothing_unattributed_emits_no_notice() =>
        Assert.Null(TierLadder.UnattributedNotice([Route("global.json")]));

    [Fact]
    public void An_unrecognised_file_with_an_ancestor_project_selects_that_project() =>
        Assert.Equal(["Billing"], Names(Route("services/billing/Billing/notes.md")));

    // ---- Tier 2 ------------------------------------------------------------------------------------

    [Fact]
    public void Tier_2_selects_every_assembly_whose_symbols_list_the_document()
    {
        var shared = new ProjectFile(Paths.Normalise("/repo/src/Shared/Shared.csproj"), "Shared", ["net10.0"], [], []);
        var linked = Paths.Normalise("/repo/src/Shared/Linked.cs");

        AssemblyInstanceWith(shared, "First", linked, out var first);
        AssemblyInstanceWith(shared, "Second", linked, out var second);
        AssemblyInstanceWith(shared, "Third", Paths.Normalise("/repo/src/Shared/Other.cs"), out var third);

        var listing = TierLadder.AssembliesListing(linked, [first, second, third]);

        // A linked file compiled into two projects belongs to both, and the lookup is the
        // inverse of the document enumeration correspondence already produced.
        Assert.Equal(2, listing.Count);
        Assert.DoesNotContain(listing, instance => instance.Assembly.SimpleName == "Third");
    }

    private static void AssemblyInstanceWith(
        ProjectFile project,
        string assemblyName,
        string documentPath,
        out Reach.Assemblies.AssemblyInstance instance) =>
        instance = new Reach.Assemblies.AssemblyInstance(
            new ExpectedAssemblyInstance(project, "net10.0"),
            new Reach.Assemblies.ScannedAssembly(
                Paths.Normalise($"/repo/bin/{assemblyName}.dll"),
                assemblyName,
                Guid.NewGuid(),
                Reach.Assemblies.TargetFrameworkMoniker.Parse("net10.0"),
                IsFirstParty: true,
                [new Reach.Assemblies.SourceDocument(documentPath, Guid.Empty, [])]));
}
