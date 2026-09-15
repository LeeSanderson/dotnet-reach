using Reach.Graph;
using Reach.Selection;

namespace Reach.Tests.Selection;

/// <summary>
/// The walk over hand-built edges. Widened edges do not exist yet — widening is a later ticket
/// — so the walk's handling of them is asserted here, before any widened edge is produced, on
/// a graph written by hand.
/// </summary>
public class ReverseWalkTests
{
    private static MethodId Node(int token) => MethodId.Definition(0, token);

    private static readonly MethodId Change = Node(1);
    private static readonly MethodId Middle = Node(2);
    private static readonly MethodId Test = Node(3);
    private static readonly MethodId Unrelated = Node(4);

    [Fact]
    public void A_change_two_hops_from_a_test_reaches_it()
    {
        var graph = CallGraph.Build(
            [Change, Middle, Test, Unrelated],
            [
                new Edge(Test, Middle, EdgeProvenance.CompiledCall),
                new Edge(Middle, Change, EdgeProvenance.CompiledCall),
            ]);

        var reached = ReverseWalk.From(graph, [Change]);

        Assert.Equal(PathClass.Compiled, reached[Test]);
        Assert.DoesNotContain(Unrelated, reached.Keys);
    }

    [Fact]
    public void A_test_reachable_only_through_a_widened_edge_reads_widened()
    {
        var graph = CallGraph.Build(
            [Change, Test],
            [new Edge(Test, Change, EdgeProvenance.Widened)]);

        Assert.Equal(PathClass.Widened, ReverseWalk.From(graph, [Change])[Test]);
    }

    [Fact]
    public void A_test_with_both_a_compiled_and_a_widened_path_reads_compiled()
    {
        var graph = CallGraph.Build(
            [Change, Middle, Test],
            [
                // The widened path is shorter, so anything taking the first path found, or the
                // worst edge seen, gets this backwards.
                new Edge(Test, Change, EdgeProvenance.Widened),
                new Edge(Test, Middle, EdgeProvenance.CompiledCall),
                new Edge(Middle, Change, EdgeProvenance.CompiledCall),
            ]);

        // The weakest provenance on the *strongest* path. `widened` means "over-selection is
        // plausible here", and a test that is also reachable by compiled code is not that.
        Assert.Equal(PathClass.Compiled, ReverseWalk.From(graph, [Change])[Test]);
    }

    [Fact]
    public void A_synthesised_edge_counts_as_compiled()
    {
        var graph = CallGraph.Build(
            [Change, Test],
            [new Edge(Test, Change, EdgeProvenance.Containment)]);

        // Containment and type initialization are invented by Reach, but the control flow they
        // describe is not in doubt, so they are as non-removable as compiled edges.
        Assert.Equal(PathClass.Compiled, ReverseWalk.From(graph, [Change])[Test]);
    }

    [Fact]
    public void A_path_that_crosses_a_widened_edge_and_then_compiled_ones_is_still_widened()
    {
        var graph = CallGraph.Build(
            [Change, Middle, Test],
            [
                new Edge(Middle, Change, EdgeProvenance.Widened),
                new Edge(Test, Middle, EdgeProvenance.CompiledCall),
            ]);

        Assert.Equal(PathClass.Widened, ReverseWalk.From(graph, [Change])[Test]);
    }

    [Fact]
    public void A_cycle_terminates()
    {
        var graph = CallGraph.Build(
            [Change, Middle, Test],
            [
                new Edge(Middle, Change, EdgeProvenance.CompiledCall),
                new Edge(Change, Middle, EdgeProvenance.CompiledCall),
                new Edge(Test, Middle, EdgeProvenance.CompiledCall),
            ]);

        Assert.Contains(Test, ReverseWalk.From(graph, [Change]).Keys);
    }

    [Fact]
    public void The_roots_themselves_are_reached()
    {
        var graph = CallGraph.Build([Change], []);

        Assert.Equal(PathClass.Compiled, ReverseWalk.From(graph, [Change])[Change]);
    }

    [Fact]
    public void A_hop_by_hop_path_runs_from_the_test_back_to_the_root()
    {
        var graph = CallGraph.Build(
            [Change, Middle, Test],
            [
                new Edge(Test, Middle, EdgeProvenance.CompiledCall),
                new Edge(Middle, Change, EdgeProvenance.CompiledCall),
            ]);

        var predecessors = ReverseWalk.Predecessors(graph, [Change]);

        // Reconstructed backwards over the reverse index, which is the only direction the
        // graph is indexed in.
        Assert.Equal([Test, Middle, Change], ReverseWalk.PathFrom(predecessors, Test));
    }

    [Fact]
    public void A_hop_by_hop_path_over_a_cycle_terminates()
    {
        var graph = CallGraph.Build(
            [Change, Middle],
            [
                new Edge(Middle, Change, EdgeProvenance.CompiledCall),
                new Edge(Change, Middle, EdgeProvenance.CompiledCall),
            ]);

        var predecessors = ReverseWalk.Predecessors(graph, [Change]);

        Assert.NotEmpty(ReverseWalk.PathFrom(predecessors, Middle));
    }
}
