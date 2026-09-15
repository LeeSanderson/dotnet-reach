using Reach.Graph;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Graph;

/// <summary>
/// Widening, over real IL compiled from source strings. Every assertion here is about fan-out:
/// too little is under-selection, and too much is a graph with no information in it.
/// </summary>
public class WideningTests
{
    // ---- The assertion the whole ticket exists for ---------------------------------------------

    [Fact]
    public void ToString_on_a_type_declaring_an_override_widens_to_that_types_override_only()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Square { public override string ToString() => "sq"; }
            public class Circle { public override string ToString() => "ci"; }
            public class Triangle : Square { public override string ToString() => "tr"; }

            public class Caller
            {
                public string Describe()
                {
                    var square = new Square();
                    return square.ToString();
                }
            }
            """);

        var describe = graphs.Method("N.Caller", "Describe");

        // Roslyn emits `callvirt System.Object::ToString()` here — verified by disassembly, not
        // assumed. Taking that token at face value would edge to one object::ToString node that
        // widens to every override anywhere, so changing any one override would reverse-reach
        // almost the entire suite.
        Assert.True(Reaches(graphs, describe, graphs.Method("N.Square", "ToString")));
        Assert.True(Reaches(graphs, describe, graphs.Method("N.Triangle", "ToString")));

        // Circle is not below Square, so it is not reachable.
        Assert.False(Reaches(graphs, describe, graphs.Method("N.Circle", "ToString")));
    }

    [Fact]
    public void A_sealed_types_using_widens_to_that_types_Dispose()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public sealed class Resource : System.IDisposable { public void Dispose() { } }
            public sealed class Other : System.IDisposable { public void Dispose() { } }

            public class Caller
            {
                public void Use()
                {
                    using var resource = new Resource();
                }
            }
            """);

        var use = graphs.Method("N.Caller", "Use");

        // `using var r = new Res()` on a sealed class emits `callvirt IDisposable::Dispose()`
        // rather than a direct call, so without inference this would reach every IDisposable.
        Assert.True(Reaches(graphs, use, graphs.Method("N.Resource", "Dispose")));
        Assert.False(Reaches(graphs, use, graphs.Method("N.Other", "Dispose")));
    }

    // ---- The ladder ------------------------------------------------------------------------------

    [Fact]
    public void Rung_1_a_newobj_receiver_is_an_exact_type()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Base { public virtual int Go() => 0; }
            public class Left : Base { public override int Go() => 1; }
            public class Right : Base { public override int Go() => 2; }

            public class Caller
            {
                public int Run() => new Left().Go();
            }
            """);

        var run = graphs.Method("N.Caller", "Run");

        Assert.True(Reaches(graphs, run, graphs.Method("N.Left", "Go")));
        Assert.False(Reaches(graphs, run, graphs.Method("N.Right", "Go")));
    }

    [Fact]
    public void Rung_2_an_ldsfld_receiver_uses_the_fields_signature_type()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Base { public virtual int Go() => 0; }
            public class Left : Base { public override int Go() => 1; }
            public class Right : Base { public override int Go() => 2; }

            public class Caller
            {
                private static readonly Left Field = new();

                public int Run() => Field.Go();
            }
            """);

        var run = graphs.Method("N.Caller", "Run");

        Assert.True(Reaches(graphs, run, graphs.Method("N.Left", "Go")));
        Assert.False(Reaches(graphs, run, graphs.Method("N.Right", "Go")));
    }

    [Fact]
    public void Rung_2_an_ldarg_receiver_uses_the_parameters_signature_type()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Base { public virtual int Go() => 0; }
            public class Left : Base { public override int Go() => 1; }
            public class Right : Base { public override int Go() => 2; }

            public class Caller
            {
                public int Run(Left value) => value.Go();
            }
            """);

        var run = graphs.Method("N.Caller", "Run");

        Assert.True(Reaches(graphs, run, graphs.Method("N.Left", "Go")));
        Assert.False(Reaches(graphs, run, graphs.Method("N.Right", "Go")));
    }

    [Fact]
    public void Rung_3_a_constrained_generic_receiver_picks_the_constraint_from_the_token()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public interface IShape { int Area(); }
            public interface IOther { int Volume(); }

            public class Square : IShape { public int Area() => 1; }
            public class Cube : IOther { public int Volume() => 2; }

            public class Caller
            {
                public int Measure<T>(T shape) where T : IShape => shape.Area();
            }
            """);

        var measure = graphs.Method("N.Caller", "Measure");

        // `constrained. !!T; callvirt IShape::Area()` — Roslyn puts the constraint in the
        // token, so inference gets it without reading the constraint table.
        Assert.True(Reaches(graphs, measure, graphs.Method("N.Square", "Area")));
        Assert.False(Reaches(graphs, measure, graphs.Method("N.Cube", "Volume")));
    }

    [Fact]
    public void Rung_4_an_unconstrained_generic_receiver_falls_through_to_the_slot()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Square { public override string ToString() => "sq"; }
            public class Circle { public override string ToString() => "ci"; }

            public class Caller
            {
                public string Describe<T>(T value) => value!.ToString()!;
            }
            """);

        var describe = graphs.Method("N.Caller", "Describe");

        // `constrained. !!T` with the token at object::ToString, and identity erases T — so
        // this one site keeps full fan-out. A registered residue, asserted so it stays a
        // decision rather than becoming a surprise.
        Assert.True(Reaches(graphs, describe, graphs.Method("N.Square", "ToString")));
        Assert.True(Reaches(graphs, describe, graphs.Method("N.Circle", "ToString")));
    }

    // ---- Mapping ---------------------------------------------------------------------------------

    [Fact]
    public void An_explicit_interface_implementation_is_reached_through_MethodImpl()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public interface IShape { int Area(); }

            public class Square : IShape
            {
                // Explicitly implemented, so its IL name is N.IShape.Area — deliberately not
                // name-matchable.
                int IShape.Area() => 1;
            }

            public class Caller
            {
                public int Measure(IShape shape) => shape.Area();
            }
            """);

        Assert.True(
            Reaches(graphs, graphs.Method("N.Caller", "Measure"), graphs.Method("N.Square", "N.IShape.Area")));
    }

    [Fact]
    public void A_default_interface_method_is_reached_alongside_overrides()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public interface IShape { int Area() => 0; }

            public class Square : IShape { public int Area() => 1; }
            public class Blank : IShape { }

            public class Caller
            {
                public int Measure(IShape shape) => shape.Area();
            }
            """);

        var measure = graphs.Method("N.Caller", "Measure");

        // A default interface method is a real body on the interface, so it is reachable
        // alongside every override rather than instead of them.
        Assert.True(Reaches(graphs, measure, graphs.Method("N.IShape", "Area")));
        Assert.True(Reaches(graphs, measure, graphs.Method("N.Square", "Area")));
    }

    [Fact]
    public void A_static_abstract_interface_member_widens_to_every_implementer()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public interface IParser<T> where T : IParser<T>
            {
                static abstract T Parse(string text);
            }

            public class Money : IParser<Money> { public static Money Parse(string text) => new(); }
            public class Weight : IParser<Weight> { public static Weight Parse(string text) => new(); }

            public class Caller
            {
                public T Read<T>(string text) where T : IParser<T> => T.Parse(text);
            }
            """);

        var read = graphs.Method("N.Caller", "Read");

        // The implementation is selected by the generic instantiation at the call site, which
        // method identity discards — so nothing narrower is available, and widening is the safe
        // direction. The most visible cost of collapsing instantiations.
        Assert.True(Reaches(graphs, read, graphs.Method("N.Money", "Parse")));
        Assert.True(Reaches(graphs, read, graphs.Method("N.Weight", "Parse")));
    }

    [Fact]
    public void An_interface_typed_parameter_infers_to_the_interface()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public interface IRepo { void Save(); }

            public class SqlRepo : IRepo { public void Save() { } }
            public class FileRepo : IRepo { public void Save() { } }

            public class Service
            {
                public void Run(IRepo repo) => repo.Save();
            }
            """);

        var run = graphs.Method("N.Service", "Run");

        // Correct and useless: the dependency-injection shape *is* an interface-typed receiver,
        // so receiver-type inference does not touch it. Asserted so nobody later believes this
        // ticket solved that case.
        Assert.True(Reaches(graphs, run, graphs.Method("N.SqlRepo", "Save")));
        Assert.True(Reaches(graphs, run, graphs.Method("N.FileRepo", "Save")));
    }

    // ---- Residues ---------------------------------------------------------------------------------

    [Fact]
    public void A_stack_merge_keeps_full_fan_out_at_that_site()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Base { public virtual int Go() => 0; }
            public class Left : Base { public override int Go() => 1; }
            public class Right : Base { public override int Go() => 2; }

            public class Caller
            {
                public int Run(bool which) => (which ? (Base)new Left() : new Right()).Go();
            }
            """);

        var run = graphs.Method("N.Caller", "Run");

        // A ternary compiles to branches pushing different types, and everything before a
        // branch target stops being knowable — so this one site keeps full fan-out. Errs over,
        // and is asserted so the residue stays a decision.
        Assert.True(Reaches(graphs, run, graphs.Method("N.Left", "Go")));
        Assert.True(Reaches(graphs, run, graphs.Method("N.Right", "Go")));
    }

    // ---- Provenance and cost -------------------------------------------------------------------------

    [Fact]
    public void Every_edge_widening_creates_carries_widened_provenance()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public interface IRepo { void Save(); }
            public class SqlRepo : IRepo { public void Save() { } }

            public class Service
            {
                public void Run(IRepo repo) => repo.Save();
            }
            """);

        // The interface node is what the call site reaches; the implementation hangs off it.
        Assert.Equal(
            [EdgeProvenance.Widened],
            graphs.EdgesBetween(graphs.Method("N.IRepo", "Save"), graphs.Method("N.SqlRepo", "Save")));

        // And the call site's own edge stays compiled.
        Assert.Equal(
            [EdgeProvenance.CompiledCall],
            graphs.EdgesBetween(graphs.Method("N.Service", "Run"), graphs.Method("N.IRepo", "Save")));
    }

    [Fact]
    public void Ldvirtftn_produces_widened_edges_as_well_as_its_capture()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Base { public virtual int Go() => 0; }
            public class Derived : Base { public override int Go() => 1; }

            public class Caller
            {
                public System.Func<int> Capture(Base value) => value.Go;
            }
            """);

        var capture = graphs.Method("N.Caller", "Capture");

        Assert.True(Reaches(graphs, capture, graphs.Method("N.Derived", "Go")));
    }

    [Fact]
    public void The_type_hierarchy_index_is_built_exactly_once_per_run()
    {
        using var graphs = Graphs.Of(
            ("Core", "namespace N; public interface I { void M(); } public class A : I { public void M() { } }"),
            ("App", "namespace N; public class B : I { public void M() { } }"));

        // One of M1's two committed algorithmic constraints. Resolving implementations per call
        // site is accidentally quadratic and will look fine on a sample repository and fail on
        // a client's.
        Assert.Equal(1, graphs.Result.HierarchyConstructions);
    }

    /// <summary>Whether <paramref name="from"/> can reach <paramref name="to"/> at all.</summary>
    private static bool Reaches(Graphs graphs, MethodId from, MethodId to) =>
        Reach.Selection.ReverseWalk.From(graphs.Graph, [to]).ContainsKey(from);
}
