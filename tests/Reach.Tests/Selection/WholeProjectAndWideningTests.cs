using Reach.Changes;
using Reach.Graph;
using Reach.Join;
using Reach.Projects;
using Reach.Reporting;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Selection;

/// <summary>
/// The two responses to missing information — whole-project selection and whole-assembly
/// widening — and the one property that keeps the report readable when they fire.
/// </summary>
public class WholeProjectAndWideningTests
{
    [Fact]
    public void An_unrecognised_framework_runs_the_whole_project_with_an_unknown_total()
    {
        using var selections = Selections.Of(
            "namespace N; public class Widget { public int Spin() => 1; }",
            """
            namespace N.Tests;

            public sealed class ContosoTestAttribute : System.Attribute { }

            public class WidgetTests
            {
                [ContosoTest]
                public void Spins() => new N.Widget().Spin();
            }
            """,
            changed: ["Widget.Spin"]);

        var project = selections.TestProject;

        Assert.Equal(SelectionMode.RunAll, project.Mode);

        // Unknown, never zero: reporting zero would silently corrupt the over-selection ratio
        // in exactly the case where Reach is running an entire project.
        Assert.Null(project.Total);

        Assert.Contains(selections.Result.Notices, notice => notice.Code == NoticeCodes.WholeProjectFallback);
    }

    [Fact]
    public void Whole_assembly_widening_contributes_one_entry_whatever_it_expands_to()
    {
        using var graphs = Graphs.Of(
            ("Core",
                """
                namespace N;

                public class A { public int M1() => 1; public int M2() => 2; }
                public class B { public int M3() => 3; public int M4() => 4; }
                public class C { public int M5() => 5; }
                """));

        var project = new ProjectFile(Paths.Normalise("/repo/src/Core/Core.csproj"), "Core", ["net10.0"], [], []);

        var roots = new RootSets(graphs.Assemblies);

        var changes = roots.ChangesFrom(
            new ChangedSet(
                [],
                [],
                [new AssemblyWidening("N.A", WideningReason.DeletedType, "src/Core/A.cs", project)],
                [],
                [],
                []),
            [],
            new AnalysisScope([project], [project], []),
            new TierLadder(new AnalysisScope([project], [project], []), "/repo"));

        var change = Assert.Single(changes);

        // One unattributable file would otherwise expand into one entry per method, and every
        // test in that assembly's dependents would carry all of them.
        Assert.Equal(ChangeTier.WholeAssembly, change.Entry.Tier);
        Assert.Equal("N.A", change.Entry.Display);

        // The expansion stays an implementation detail of the walk.
        Assert.True(change.Roots.Count >= 5, $"expected every method of Core, got {change.Roots.Count}");
    }

    [Fact]
    public void Whole_type_widening_covers_the_types_generated_members_too()
    {
        using var graphs = Graphs.Of(
            ("Core",
                """
                namespace N;

                public class A
                {
                    public async System.Threading.Tasks.Task<int> Work()
                    {
                        await System.Threading.Tasks.Task.Yield();
                        return 1;
                    }
                }
                """));

        var roots = new RootSets(graphs.Assemblies);

        // The state machine lives in N.A+<Work>d__0, and whole-type widening has to cover the
        // code that actually runs.
        Assert.True(roots.InType("N.A").Count > roots.InType("N.A+<Work>d__0").Count);
        Assert.NotEmpty(roots.InType("N.A+<Work>d__0"));
    }

    [Fact]
    public void Widening_a_test_assembly_selects_the_tests_in_it()
    {
        // An interface member has no IL and no sequence points, so it cannot join and widens
        // its whole assembly — which here is the test assembly itself. Found by dogfooding:
        // two `const` fields in a test class did exactly this and selected nothing at all.
        using var selections = Selections.Of(
            "namespace N; public class Widget { public int Spin() => 1; }",
            """
            namespace N.Tests;

            public interface IMarker { int Describe(); }

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Spins() => new N.Widget().Spin();

                [Xunit.Fact]
                public void Also_spins() => new N.Widget().Spin();
            }
            """,
            changed: ["IMarker.Describe"]);

        var change = Assert.Single(selections.Changes);

        Assert.Equal(ChangeTier.WholeAssembly, change.Tier);

        // The whole point of widening is that it over-selects. A test that is itself one of the
        // widening's roots is still selected by it — the direction that fails safely.
        Assert.Equal(["Also_spins", "Spins"], selections.SelectedNames);
        Assert.Equal(2, change.TestsReached);
    }

    [Fact]
    public void A_declaration_that_fails_to_join_falls_through_to_whole_assembly_widening()
    {
        using var graphs = Graphs.Of(("Core", "namespace N; public class A { public int M() => 1; }"));

        var project = new ProjectFile(Paths.Normalise("/repo/src/Core/Core.csproj"), "Core", ["net10.0"], [], []);
        var roots = new RootSets(graphs.Assemblies);

        var unjoined = new ChangedMember(
            "N.Ghost",
            new MemberKey(MemberKind.Method, "Vanished", 0, []),
            MemberChange.Modified,
            "src/Core/Ghost.cs");

        var changes = roots.ChangesFrom(
            ChangedSet.Empty,
            [new JoinResult(unjoined, [])],
            new AnalysisScope([project], [project], []),
            new TierLadder(new AnalysisScope([project], [project], []), "/repo"));

        var change = Assert.Single(changes);

        // Over-selection, and it is what makes a phantom member from a misparse survivable
        // rather than a silent hole.
        Assert.Equal(ChangeTier.WholeAssembly, change.Entry.Tier);
        Assert.NotEmpty(change.Roots);
    }
}
