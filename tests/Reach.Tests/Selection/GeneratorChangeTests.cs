using Reach.Changes;
using Reach.Join;
using Reach.Projects;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Selection;

/// <summary>
/// Rule 4 of the tier ladder for every kind of change inside a generator project, not only its
/// project file. The generator's assembly is consumed by the compiler rather than loaded, so it
/// is never in the graph: what has to widen is every project that consumes it.
/// </summary>
public class GeneratorChangeTests
{
    private const string GeneratorReason = "a source generator or analyser project changed";

    private static readonly ProjectFile Generator =
        new(Paths.Normalise("/repo/src/Generator/Generator.csproj"), "Generator", ["netstandard2.0"], [], []);

    private static readonly ProjectFile Core = new(
        Paths.Normalise("/repo/src/Core/Core.csproj"),
        "Core",
        ["net10.0"],
        [new ProjectReference(Generator.Path, ReferenceOutputAssembly: false, IsAnalyzer: true)],
        []);

    private static readonly AnalysisScope Scope = new([Generator, Core], [Generator, Core], []);

    private static readonly ChangedMember MarkerName = new(
        "Gen.Marker",
        new MemberKey(MemberKind.Property, "Name", 0, []),
        MemberChange.Modified,
        "src/Generator/Marker.cs");

    [Fact]
    public void A_changed_member_of_a_generator_widens_its_consumers()
    {
        using var graphs = CoreOnly();
        var roots = new RootSets(graphs.Assemblies);

        var change = Assert.Single(ChangesFrom(
            roots,
            ChangedSet.Empty with { Members = [MarkerName] },
            [new JoinResult(MarkerName, [])]));

        Assert.Equal("Gen.Marker.Name", change.Entry.Display);
        Assert.Equal(ChangeTier.WholeAssembly, change.Entry.Tier);
        Assert.Equal(GeneratorReason, change.Entry.Reason);
        Assert.Equal(roots.InAssembly("Core"), change.Roots);
    }

    [Fact]
    public void A_new_type_in_a_generator_widens_its_consumers()
    {
        using var graphs = CoreOnly();
        var roots = new RootSets(graphs.Assemblies);

        var added = MarkerName with { DeclaringType = "Gen.Fresh", Change = MemberChange.Added, Path = "src/Generator/Fresh.cs" };

        var change = Assert.Single(ChangesFrom(
            roots,
            ChangedSet.Empty with { Members = [added] },
            [new JoinResult(added, [])]));

        Assert.Equal(GeneratorReason, change.Entry.Reason);
        Assert.Equal(roots.InAssembly("Core"), change.Roots);
    }

    [Fact]
    public void A_deleted_file_in_a_generator_widens_its_consumers()
    {
        using var graphs = CoreOnly();
        var roots = new RootSets(graphs.Assemblies);

        var change = Assert.Single(ChangesFrom(
            roots,
            ChangedSet.Empty with
            {
                AssemblyWidenings =
                [
                    new AssemblyWidening("Gen.Marker", WideningReason.DeletedType, "src/Generator/Marker.cs", Generator),
                ],
            },
            []));

        Assert.Equal("Gen.Marker", change.Entry.Display);
        Assert.Equal(GeneratorReason, change.Entry.Reason);
        Assert.Equal(roots.InAssembly("Core"), change.Roots);
    }

    [Fact]
    public void A_type_widening_in_a_generator_widens_its_consumers()
    {
        using var graphs = CoreOnly();
        var roots = new RootSets(graphs.Assemblies);

        var change = Assert.Single(ChangesFrom(
            roots,
            ChangedSet.Empty with
            {
                TypeWidenings = [new TypeWidening("Gen.Marker", WideningReason.TypeHeaderChanged, "src/Generator/Marker.cs")],
            },
            []));

        Assert.Equal(ChangeTier.WholeAssembly, change.Entry.Tier);
        Assert.Equal(GeneratorReason, change.Entry.Reason);
        Assert.Equal(roots.InAssembly("Core"), change.Roots);
    }

    [Fact]
    public void A_changed_member_outside_a_generator_is_not_routed_to_consumers()
    {
        using var graphs = CoreOnly();
        var roots = new RootSets(graphs.Assemblies);

        var spin = MarkerName with { DeclaringType = "N.Widget", Path = "src/Core/Widget.cs" };

        var change = Assert.Single(ChangesFrom(roots, ChangedSet.Empty with { Members = [spin] }, [new JoinResult(spin, [])]));

        // The ordinary fall-through, with its own reason.
        Assert.NotEqual(GeneratorReason, change.Entry.Reason);
    }

    [Fact]
    public void A_props_file_inside_a_generator_widens_its_consumers()
    {
        using var graphs = CoreOnly();
        var roots = new RootSets(graphs.Assemblies);

        var change = Assert.Single(ChangesFrom(
            roots,
            ChangedSet.Empty with
            {
                UnmappablePaths = [new ChangedPath("src/Generator/Directory.Build.props", ChangeStatus.Modified)],
            },
            []));

        Assert.Equal(ChangeTier.WholeAssembly, change.Entry.Tier);
        Assert.Equal(roots.InAssembly("Core"), change.Roots);
    }

    private static Graphs CoreOnly() =>
        Graphs.Of(("Core", "namespace N; public class Widget { public int Spin() => 1; }"));

    private static IReadOnlyList<Change> ChangesFrom(RootSets roots, ChangedSet changed, IReadOnlyList<JoinResult> joined) =>
        roots.ChangesFrom(changed, joined, Scope, new TierLadder(Scope, "/repo"), []);
}
