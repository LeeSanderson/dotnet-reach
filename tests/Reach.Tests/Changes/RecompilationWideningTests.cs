using Reach.Changes;
using Reach.Projects;

namespace Reach.Tests.Changes;

/// <summary>
/// The rule for a consumer whose IL changed while its source did not. Two of these assertions
/// are the shapes that under-select if it regresses, and neither produces an error when it does
/// — the test simply does not run.
/// </summary>
public class RecompilationWideningTests
{
    /// <summary>Core is referenced by App, which is referenced by Tests.</summary>
    private static AnalysisScope Scope()
    {
        var core = new ProjectFile(Paths.Normalise("/repo/src/Core/Core.csproj"), "Core", ["net10.0"], [], []);

        var app = new ProjectFile(
            Paths.Normalise("/repo/src/App/App.csproj"),
            "App",
            ["net10.0"],
            [new ProjectReference(core.Path, ReferenceOutputAssembly: true, IsAnalyzer: false)],
            []);

        var tests = new ProjectFile(
            Paths.Normalise("/repo/tests/Tests/Tests.csproj"),
            "Tests",
            ["net10.0"],
            [new ProjectReference(app.Path, ReferenceOutputAssembly: true, IsAnalyzer: false)],
            []);

        var unrelated = new ProjectFile(
            Paths.Normalise("/repo/src/Unrelated/Unrelated.csproj"),
            "Unrelated",
            ["net10.0"],
            [],
            []);

        return new AnalysisScope([core, app, tests, unrelated], [tests], []);
    }

    private static ChangedMember Member(
        MemberChange change,
        bool isConstant = false,
        string path = "src/Core/Widget.cs",
        MemberKind kind = MemberKind.Method) =>
        new("N.Widget", new MemberKey(kind, "Value", 0, []), change, path, isConstant);

    private static string[] Consumers(params ChangedMember[] members)
    {
        var scope = Scope();

        var widened = RecompilationWidening.From(
            new ChangedSet(members, [], [], [], [], []),
            scope,
            member => scope.Projects.First(project =>
                Paths.IsUnder(Paths.Normalise("/repo/" + member.Path), project.Directory)));

        return [.. widened.SelectMany(w => w.Consumers).Select(p => p.Name).Distinct().Order(StringComparer.Ordinal)];
    }

    // ---- The two triggers ---------------------------------------------------------------------

    [Fact]
    public void A_removed_member_widens_every_transitive_referencer()
    {
        // An untouched call site in another assembly rebinds to a different method, and its
        // source never changed — so nothing else would put it in the changed set.
        Assert.Equal(["App", "Tests"], Consumers(Member(MemberChange.Removed)));
    }

    [Fact]
    public void A_changed_compile_time_constant_widens_every_transitive_referencer()
    {
        // After inlining the consumer's source is byte-identical, its IL is different, and its
        // IL may no longer reference the declaring assembly at all.
        Assert.Equal(
            ["App", "Tests"],
            Consumers(Member(MemberChange.Modified, isConstant: true, kind: MemberKind.Field)));
    }

    [Fact]
    public void A_changed_enum_member_triggers() =>
        Assert.NotEmpty(Consumers(Member(MemberChange.Modified, isConstant: true, kind: MemberKind.EnumMember)));

    [Fact]
    public void A_changed_default_parameter_value_triggers() =>
        Assert.NotEmpty(Consumers(Member(MemberChange.Modified, isConstant: true)));

    // ---- What must not trigger -------------------------------------------------------------------

    [Fact]
    public void An_added_member_triggers_nothing()
    {
        // The narrow trigger is only sufficient because of this: an added member *is* in the
        // changed set and the rebound call site's recompiled IL points at it, so the reverse
        // walk finds it for free.
        Assert.Empty(Consumers(Member(MemberChange.Added)));
        Assert.Empty(Consumers(Member(MemberChange.Added, isConstant: true)));
    }

    [Fact]
    public void An_ordinary_modification_triggers_nothing() =>
        Assert.Empty(Consumers(Member(MemberChange.Modified)));

    [Fact]
    public void A_changed_attribute_argument_does_not_trigger_but_is_still_a_changed_member()
    {
        // An attribute argument lands in the declaring assembly's own metadata and is not
        // inlined into consumers, so the declaration-level hash already covers it.
        var declared = SourceRevision.DeclaredTypes(
            """
            namespace N;

            public class Widget
            {
                [System.Obsolete("why")]
                public int Value() => 1;
            }
            """);

        var member = declared["N.Widget"].Members.Values.Single();

        Assert.False(member.IsCompileTimeConstant);
        Assert.Empty(Consumers(Member(MemberChange.Modified)));
    }

    // ---- Narrowings that were rejected ---------------------------------------------------------------

    [Fact]
    public void An_internal_const_triggers_just_like_a_public_one()
    {
        // Restricting to public surface founders on InternalsVisibleTo and internal consts
        // consumed by friend assemblies, and detecting the friend relationship correctly costs
        // more than the widening saves. Nothing here reads accessibility at all, which is the
        // point.
        var declared = SourceRevision.DeclaredTypes(
            "namespace N; public class Widget { internal const int Value = 1; }");

        Assert.True(declared["N.Widget"].Members.Values.Single().IsCompileTimeConstant);
        Assert.NotEmpty(Consumers(Member(MemberChange.Modified, isConstant: true, kind: MemberKind.Field)));
    }

    // ---- Shape --------------------------------------------------------------------------------------

    [Fact]
    public void Widening_follows_the_project_graph_and_reaches_nothing_unrelated() =>
        Assert.DoesNotContain("Unrelated", Consumers(Member(MemberChange.Removed)));

    [Fact]
    public void A_declaring_project_nothing_references_widens_nothing()
    {
        Assert.Empty(Consumers(Member(MemberChange.Removed, path: "tests/Tests/WidgetTests.cs")));
    }

    [Fact]
    public void The_blast_radius_is_attributed_to_the_member_that_caused_it()
    {
        var scope = Scope();
        var trigger = Member(MemberChange.Modified, isConstant: true, kind: MemberKind.Field);

        var widened = RecompilationWidening.From(
            new ChangedSet([trigger], [], [], [], [], []),
            scope,
            _ => scope.Projects[0]);

        // The cost is accepted because it is legible: a pull request that selected everything
        // shows exactly which const did it.
        var one = Assert.Single(widened);

        Assert.Equal(trigger, one.Trigger);
        Assert.Contains("compile-time constant", one.Reason);
    }
}
