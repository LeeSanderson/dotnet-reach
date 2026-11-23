using Reach.Changes;
using Reach.Join;
using Reach.Projects;
using Reach.Reporting;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Selection;

/// <summary>
/// Changed paths are relative to the repository, so the project a changed file belongs to is
/// found against the repository, not against wherever the in-scope projects happen to sit. Every
/// in-scope project here is below <c>/repo/app/</c>, which is the shape that went wrong.
/// </summary>
public class ProjectContainmentTests
{
    private const string Root = "/repo";

    private static readonly ProjectFile Core =
        new(Paths.Normalise("/repo/app/src/Core/Core.csproj"), "Core", ["net10.0"], [], []);

    private static readonly ProjectFile Api = new(
        Paths.Normalise("/repo/app/src/Api/Api.csproj"),
        "Api",
        ["net10.0"],
        [new ProjectReference(Core.Path, ReferenceOutputAssembly: true, IsAnalyzer: false)],
        []);

    private static readonly ProjectFile Tests = new(
        Paths.Normalise("/repo/app/tests/Api.Tests/Api.Tests.csproj"),
        "Api.Tests",
        ["net10.0"],
        [new ProjectReference(Api.Path, ReferenceOutputAssembly: true, IsAnalyzer: false)],
        []);

    [Fact]
    public void An_unjoined_member_widens_its_own_assembly()
    {
        using var graphs = Graphs.Of(
            ("Core", "namespace N; public class A { public int M() => 1; }"),
            ("Api", "namespace N.Api; public class B { public int M() => 2; }"));

        var scope = new AnalysisScope([Core, Api, Tests], [Tests], []);
        var roots = new RootSets(graphs.Assemblies);

        var unjoined = new ChangedMember(
            "N.A",
            new MemberKey(MemberKind.Method, "Vanished", 0, []),
            MemberChange.Modified,
            "app/src/Core/A.cs");

        var change = Assert.Single(roots.ChangesFrom(
            ChangedSet.Empty with { Members = [unjoined] },
            [new JoinResult(unjoined, [])],
            scope,
            new TierLadder(scope, Root),
            []));

        Assert.Equal(ChangeTier.WholeAssembly, change.Entry.Tier);
        Assert.Equal(roots.InAssembly("Core"), change.Roots);
    }

    [Fact]
    public void A_changed_constant_widens_its_transitive_referencers()
    {
        using var graphs = Graphs.Of(
            ("Core", "namespace N; public class A { public const int Value = 2; }"),
            ("Api", "namespace N.Api; public class B { public int M() => N.A.Value; }"));

        var scope = new AnalysisScope([Core, Api, Tests], [Tests], []);
        var notices = new List<Notice>();

        var constant = new ChangedMember(
            "N.A",
            new MemberKey(MemberKind.Field, "Value", 0, []),
            MemberChange.Modified,
            "app/src/Core/A.cs",
            ChangesCompileTimeConstant: true);

        new RootSets(graphs.Assemblies).ChangesFrom(
            ChangedSet.Empty with { Members = [constant] },
            [],
            scope,
            new TierLadder(scope, Root),
            notices);

        // Inlining can erase the reference the reverse walk would follow, so Api has to widen.
        var widening = Assert.Single(notices, notice => notice.Code == NoticeCodes.RecompilationWidening);

        Assert.Contains(Api.Path, Assert.IsType<string[]>(widening.Data!["projects"]));
    }

    [Fact]
    public void A_single_in_scope_project_does_not_claim_a_file_outside_its_directory()
    {
        using var graphs = Graphs.Of(("Core", "namespace N; public class A { public int M() => 1; }"));

        var scope = new AnalysisScope([Core], [Core], []);
        var roots = new RootSets(graphs.Assemblies);

        var elsewhere = new ChangedMember(
            "Tools.Script",
            new MemberKey(MemberKind.Method, "Run", 0, []),
            MemberChange.Modified,
            "tools/Script/Script.cs");

        var change = Assert.Single(roots.ChangesFrom(
            ChangedSet.Empty with { Members = [elsewhere] },
            [new JoinResult(elsewhere, [])],
            scope,
            new TierLadder(scope, Root),
            []));

        // No in-scope project compiles it, so it cannot reach a test: outside the scope, not Core.
        Assert.Empty(change.Roots);
        Assert.True(change.Entry.OutsideAnalysisScope);
    }
}
