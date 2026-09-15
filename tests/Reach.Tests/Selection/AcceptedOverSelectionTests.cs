using Reach.Tests.Fixtures;

namespace Reach.Tests.Selection;

/// <summary>
/// Over-selection Reach accepts on purpose. Each of these is asserted so the decision stays a
/// decision rather than drifting into a bug report someone closes by "fixing" it.
/// </summary>
public class AcceptedOverSelectionTests
{
    [Fact]
    public void A_change_reachable_only_through_Handler_of_Foo_selects_tests_using_only_Handler_of_Bar()
    {
        using var selections = Selections.Of(
            """
            namespace N;

            public class Foo { }
            public class Bar { }

            public class Handler<T>
            {
                public int Handle(T item) => Shared();

                public int Shared() => 1;
            }
            """,
            """
            namespace N.Tests;

            public class HandlerTests
            {
                [Xunit.Fact]
                public void Handles_a_Bar() => new N.Handler<N.Bar>().Handle(new N.Bar());
            }
            """,
            changed: ["Handler`1.Handle"]);

        // Generic instantiations collapse to one node per definition, so a change inside
        // Handler<Foo>.Handle is the same node as Handler<Bar>.Handle. The type argument
        // remains readable at the call site, so a later framework model can use it without
        // reversing this — but today it is over-selection, and it is accepted.
        Assert.Equal(["Handles_a_Bar"], selections.SelectedNames);
    }

    [Fact]
    public void A_deleted_method_widens_its_whole_type_even_when_nothing_referenced_it()
    {
        using var selections = Selections.Of(
            """
            namespace N;

            public class Widget
            {
                public int Spin() => 1;
                public int Wobble() => 2;
            }
            """,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                [Xunit.Fact]
                public void Spins() => new N.Widget().Spin();
            }
            """,
            changed: ["Widget.Spin"]);

        // The absorption test that would prove most deletions inert is designed, registered and
        // deliberately not adopted: a proof in five clauses, each a place to be wrong in the
        // under-selecting direction.
        Assert.Equal(["Spins"], selections.SelectedNames);
    }
}
