using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Selection;

/// <summary>
/// The four selection rules, and the forward change list, end to end over real IL.
/// </summary>
public class SelectionTests
{
    private const string Core = """
        namespace N;

        public class Widget
        {
            public int Spin() => Inner();
            public int Inner() => 1;
            public int Untouched() => 2;
        }
        """;

    private const string Tests = """
        namespace N.Tests;

        public class WidgetTests
        {
            [Xunit.Fact]
            public void Spins() => new N.Widget().Spin();

            [Xunit.Fact]
            public void Untouched() => new N.Widget().Untouched();

            public void NotATest() => new N.Widget().Inner();
        }
        """;

    // ---- Rule 1: reverse-reachable -----------------------------------------------------------

    [Fact]
    public void A_change_two_hops_from_a_test_selects_it()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        // Spins calls Spin calls Inner. Untouched reaches neither.
        Assert.Equal(["Spins"], selections.SelectedNames);

        var selected = Assert.Single(selections.TestProject.Selected);
        Assert.Equal([SelectionRule.ReverseReachable], selected.Rules);
        Assert.Equal(PathClass.Compiled, selected.Class);
        Assert.NotEmpty(selected.Changes);
    }

    [Fact]
    public void A_method_that_is_not_a_test_is_never_selected()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        // NotATest calls Inner too, and carries no attribute.
        Assert.DoesNotContain("NotATest", selections.SelectedNames);
    }

    [Fact]
    public void A_change_reaching_nothing_selects_nothing()
    {
        using var selections = Selections.Of(
            Core,
            Tests,
            changed: ["Widget.Untouched"]);

        Assert.Equal(["Untouched"], selections.SelectedNames);
    }

    // ---- Rules 2 and 3 ------------------------------------------------------------------------

    [Fact]
    public void A_changed_test_method_selects_itself()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["WidgetTests.Spins"]);

        var selected = Assert.Single(selections.TestProject.Selected);

        Assert.Equal("Spins", selected.Test.Name);
        Assert.Contains(SelectionRule.OwnSourceChanged, selected.Rules);
    }

    [Fact]
    public void A_newly_added_test_selects_with_new_since_baseline_and_an_empty_roots_list()
    {
        using var selections = Selections.Of(Core, Tests, added: ["WidgetTests.Spins"]);

        var selected = Assert.Single(selections.TestProject.Selected);

        Assert.Contains(SelectionRule.NewSinceBaseline, selected.Rules);
        Assert.DoesNotContain(SelectionRule.ReverseReachable, selected.Rules);

        // An empty roots list is valid exactly when reverse-reachable is absent, which is what
        // makes a bug distinguishable from a fact.
        Assert.Empty(selected.Changes);
        Assert.Null(selected.Class);
    }

    [Fact]
    public void A_test_selected_by_a_change_elsewhere_carries_a_root_and_a_class()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Spin"]);

        var selected = Assert.Single(selections.TestProject.Selected);

        Assert.Contains(SelectionRule.ReverseReachable, selected.Rules);
        Assert.NotEmpty(selected.Changes);
        Assert.NotNull(selected.Class);
    }

    // ---- The forward change list ---------------------------------------------------------------

    [Fact]
    public void A_change_reaching_no_test_at_all_appears_with_a_count_of_zero()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Unrelated() { }
            }
            """,
            changed: ["Widget.Inner"]);

        // The field that makes an under-selection visible: "I changed Widget.Inner and nothing
        // runs" is a line you read, rather than an absence you have to notice. It is only
        // trustworthy if something proves it fires.
        var entry = Assert.Single(selections.Changes);

        Assert.Contains("Inner", entry.Display);
        Assert.Equal(0, entry.TestsReached);
        Assert.Empty(selections.SelectedNames);
    }

    [Fact]
    public void A_change_that_reaches_a_test_carries_the_count()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        Assert.Equal(1, Assert.Single(selections.Changes).TestsReached);
    }

    [Fact]
    public void A_member_change_is_tiered_as_a_member()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        Assert.Equal(ChangeTier.Member, Assert.Single(selections.Changes).Tier);
    }

    // ---- Modes ------------------------------------------------------------------------------------

    [Fact]
    public void Nothing_selected_is_skip_rather_than_an_empty_filter()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Unrelated() { }
            }
            """,
            changed: ["Widget.Inner"]);

        // An empty filter runs everything, so an empty selection emits nothing at all.
        Assert.Equal(SelectionMode.Skip, selections.TestProject.Mode);
        Assert.Empty(selections.TestProject.Selected);
    }

    [Fact]
    public void A_project_with_a_recognised_framework_reports_its_total()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        Assert.Equal(2, selections.TestProject.Total);
        Assert.Equal("xunit-v3-4", selections.TestProject.Dialect);
    }

    [Fact]
    public void Every_test_is_enumerated_not_only_the_selected_ones()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        // The totals, the rendered-match computation and the measurement all read the whole
        // list, so it has to be there even when one test is selected.
        Assert.Single(selections.TestProject.Selected);
        Assert.Equal(2, selections.TestProject.Total);
    }

    // ---- Paths -----------------------------------------------------------------------------------

    [Fact]
    public void Paths_are_off_by_default()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"]);

        // Hop-by-hop paths are the one genuinely unbounded thing in the design.
        Assert.Null(Assert.Single(selections.TestProject.Selected).Paths);
    }

    [Fact]
    public void Paths_produce_a_hop_sequence_from_the_test_back_to_the_change()
    {
        using var selections = Selections.Of(Core, Tests, changed: ["Widget.Inner"], includePaths: true);

        var paths = Assert.Single(selections.TestProject.Selected).Paths;

        Assert.NotNull(paths);

        var hops = Assert.Single(paths);

        // Spins -> Spin -> Inner.
        Assert.Equal(3, hops.Count);
        Assert.All(hops, hop => Assert.False(hop.IsExternal));
    }
}
