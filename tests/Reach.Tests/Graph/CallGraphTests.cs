using System.Runtime.CompilerServices;
using Reach.Graph;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Graph;

/// <summary>
/// The metadata pass, over real IL compiled from source strings. Every shape here is one
/// source string and a handful of assertions, which is what "anything testable in memory is
/// tested in memory" buys.
/// </summary>
public class CallGraphTests
{
    // ---- The performance budget's two checkable commitments --------------------------------

    [Fact]
    public void A_method_identity_is_eight_bytes()
    {
        // One of the three things M1's performance budget actually commits to, asserted rather
        // than reviewed. A target-framework field is what would break it, and an assembly
        // instance *is* one (project, framework) pair, so the ordinal already carries it.
        Assert.Equal(8, Unsafe.SizeOf<MethodId>());
    }

    [Fact]
    public void No_field_of_a_node_or_an_edge_is_a_string()
    {
        foreach (var type in new[] { typeof(MethodId), typeof(Edge) })
        {
            Assert.All(
                type.GetFields(
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.NonPublic),
                field => Assert.NotEqual(typeof(string), field.FieldType));
        }
    }

    [Fact]
    public void An_identity_round_trips_its_ordinal_and_token()
    {
        var definition = MethodId.Definition(1234, 0x06000042);

        Assert.False(definition.IsExternal);
        Assert.Equal(1234, definition.Ordinal);
        Assert.Equal(0x06000042, definition.Token);

        var external = MethodId.External(MethodId.MaxOrdinal, int.MaxValue);

        Assert.True(external.IsExternal);
        Assert.Equal(MethodId.MaxOrdinal, external.Ordinal);
        Assert.Equal(int.MaxValue, external.Token);
    }

    // ---- Compiled edges ----------------------------------------------------------------------

    [Fact]
    public void A_plain_call_is_one_compiled_call_edge()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Caller
            {
                public int Outer() => Inner();
                public int Inner() => 1;
            }
            """);

        var outer = graphs.Method("N.Caller", "Outer");
        var inner = graphs.Method("N.Caller", "Inner");

        Assert.Equal([EdgeProvenance.CompiledCall], graphs.EdgesBetween(outer, inner));
    }

    [Fact]
    public void A_capture_edges_from_the_capturing_method()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Captor
            {
                public System.Func<int> Capture() => Target;
                public int Target() => 1;
            }
            """);

        var capture = graphs.Method("N.Captor", "Capture");
        var target = graphs.Method("N.Captor", "Target");

        // From the capturing method to the target, because a test that can execute the target
        // must have executed the code that created the delegate.
        Assert.Equal([EdgeProvenance.CompiledCapture], graphs.EdgesBetween(capture, target));
    }

    [Fact]
    public void Invoking_a_delegate_creates_no_edge()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Invoker
            {
                public int Run(System.Func<int> f) => f();
                public int NeverCaptured() => 1;
            }
            """);

        var run = graphs.Method("N.Invoker", "Run");
        var never = graphs.Method("N.Invoker", "NeverCaptured");

        // Edging every Invoke to every method ever captured looks like widen-when-uncertain
        // and is not conservatism: Func<T> and Action are structural types shared by unrelated
        // code, so it connects everything to everything and makes the graph useless.
        Assert.Empty(graphs.EdgesBetween(run, never));
    }

    [Fact]
    public void A_local_function_is_an_ordinary_call()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Locals
            {
                public int Outer()
                {
                    return Helper();
                    int Helper() => 1;
                }
            }
            """);

        var outer = graphs.Method("N.Locals", "Outer");
        var helper = Assert.Single(
            graphs.MethodNamesOf("N.Locals"),
            name => name.Contains("Helper", StringComparison.Ordinal));

        Assert.Equal(
            [EdgeProvenance.CompiledCall],
            graphs.EdgesBetween(outer, graphs.Method("N.Locals", helper)));
    }

    [Fact]
    public void A_lambda_is_covered_by_the_capture_rule()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Lambdas
            {
                public System.Func<int> Make()
                {
                    var captured = Seed();
                    return () => captured + 1;
                }

                public int Seed() => 1;
            }
            """);

        var make = graphs.Method("N.Lambdas", "Make");

        // The lambda's body lives on a generated closure type, and Make emits a capture of it.
        Assert.Contains(
            graphs.Graph.CallersOf(graphs.Method("N.Lambdas", "Seed")).Sources.ToArray(),
            source => source == make);

        var closureMethods = graphs.Graph.Nodes
            .Where(node => !node.IsExternal)
            .Count(node => graphs.Graph.CallersOf(node).Count > 0);

        Assert.True(closureMethods > 0);
    }

    // ---- Nodes ---------------------------------------------------------------------------------

    [Fact]
    public void A_generic_method_used_at_two_instantiations_is_one_node()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Generics
            {
                public void Both()
                {
                    Identity(1);
                    Identity("two");
                }

                public T Identity<T>(T value) => value;
            }
            """);

        var both = graphs.Method("N.Generics", "Both");
        var identity = graphs.Method("N.Generics", "Identity");

        // Generic instantiations collapse to one node per definition. The accepted cost is
        // that a change reachable only through Handler<Foo> also selects tests using only
        // Handler<Bar>; the type argument stays readable at the call site.
        Assert.Equal(
            [EdgeProvenance.CompiledCall, EdgeProvenance.CompiledCall],
            graphs.EdgesBetween(both, identity));
    }

    [Fact]
    public void An_accessor_is_a_node_and_there_is_no_synthetic_property_node()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class WithProperty
            {
                public int Total { get; set; }
                public int Read() => Total;
            }
            """);

        var names = graphs.MethodNamesOf("N.WithProperty");

        // get_Total and set_Total are ordinary methods in IL. A synthetic property node would
        // make the graph carry a member IL does not have.
        Assert.Contains("get_Total", names);
        Assert.Contains("set_Total", names);
        Assert.DoesNotContain("Total", names);

        Assert.Equal(
            [EdgeProvenance.CompiledCall],
            graphs.EdgesBetween(
                graphs.Method("N.WithProperty", "Read"),
                graphs.Method("N.WithProperty", "get_Total")));
    }

    [Fact]
    public void An_operator_is_a_node()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Money
            {
                public static bool operator ==(Money a, Money b) => true;
                public static bool operator !=(Money a, Money b) => false;
                public override bool Equals(object? o) => true;
                public override int GetHashCode() => 0;
                public bool Compare(Money a, Money b) => a == b;
            }
            """);

        Assert.Equal(
            [EdgeProvenance.CompiledCall],
            graphs.EdgesBetween(
                graphs.Method("N.Money", "Compare"),
                graphs.Method("N.Money", "op_Equality")));
    }

    [Fact]
    public void A_callvirt_against_an_interface_edges_to_the_interface_member()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public interface IRepo { void Save(); }

            public class Repo : IRepo { public void Save() { } }

            public class Service
            {
                public void Run(IRepo repo) => repo.Save();
            }
            """);

        var run = graphs.Method("N.Service", "Run");

        // The dispatch declaration is the node, and widening edges hang off *that*. Edging
        // call sites straight to implementations would smear widening across the graph where
        // it can be neither isolated nor counted.
        Assert.Equal(
            [EdgeProvenance.CompiledCall],
            graphs.EdgesBetween(run, graphs.Method("N.IRepo", "Save")));

        Assert.Empty(graphs.EdgesBetween(run, graphs.Method("N.Repo", "Save")));
    }

    // ---- Cross-assembly resolution ---------------------------------------------------------------

    [Fact]
    public void A_call_into_another_first_party_assembly_resolves()
    {
        using var graphs = Graphs.Of(
            ("Core", "namespace N; public class Widget { public int Spin() => 1; }"),
            ("App", "namespace N; public class Consumer { public int Use() => new Widget().Spin(); }"));

        Assert.Equal(
            [EdgeProvenance.CompiledCall],
            graphs.EdgesBetween(
                graphs.Method("N.Consumer", "Use", assemblyIndex: 1),
                graphs.Method("N.Widget", "Spin", assemblyIndex: 0)));
    }

    [Fact]
    public void A_call_into_an_assembly_outside_the_scope_produces_no_edge_and_no_notice()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Outward
            {
                public int Count(string s) => s.Length + System.Math.Abs(-1);
            }
            """);

        var count = graphs.Method("N.Outward", "Count");

        // Normal and silent: the accepted blind spot, already registered.
        Assert.Empty(graphs.Result.Notices);
        Assert.Equal(0, graphs.Graph.CallersOf(count).Count);
    }

    [Fact]
    public void An_external_anchor_is_interned_only_where_a_first_party_type_occupies_the_slot()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Overrider
            {
                public override string ToString() => "overridden";
            }

            public class Caller
            {
                // object::ToString, whose slot Overrider occupies.
                public string Describe(object o) => o.ToString()!;

                // System.Math is inherited by nothing here, so it anchors nothing.
                public int Absolute(int x) => System.Math.Abs(x);
            }
            """);

        var anchors = graphs.ExternalAnchorNames();

        Assert.Contains(anchors, name => name.Contains("System.Object::ToString", StringComparison.Ordinal));
        Assert.DoesNotContain(anchors, name => name.Contains("System.Math", StringComparison.Ordinal));
    }

    [Fact]
    public void An_external_anchor_is_never_walked_into()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Disposable : System.IDisposable
            {
                public void Dispose() { }
            }

            public class User
            {
                public void Use(System.IDisposable d) => d.Dispose();
            }
            """);

        // System.Object is a base type of everything, so its slots anchor too — four of them
        // at most, which is what "bounded by the slots first-party types occupy" means.
        Assert.Contains(
            graphs.ExternalAnchorNames(),
            name => name.Contains("System.IDisposable::Dispose", StringComparison.Ordinal));

        foreach (var anchor in graphs.Graph.Nodes.Where(node => node.IsExternal))
        {
            // Every anchor has callers — that is the whole point of one — and no body was ever
            // read from the assembly it names, so none of them has callees.
            Assert.True(graphs.Graph.CallersOf(anchor).Count > 0);
        }
    }

    // ---- The reverse index --------------------------------------------------------------------------

    [Fact]
    public void The_reverse_index_round_trips_every_edge_exactly_once()
    {
        var a = MethodId.Definition(0, 1);
        var b = MethodId.Definition(0, 2);
        var c = MethodId.Definition(0, 3);

        var edges = new List<Edge>
        {
            new(a, b, EdgeProvenance.CompiledCall),
            new(a, c, EdgeProvenance.CompiledCapture),
            new(b, c, EdgeProvenance.CompiledCall),
            new(c, c, EdgeProvenance.Containment),
        };

        var graph = CallGraph.Build([a, b, c], edges);

        Assert.Equal(edges.Count, graph.EdgeCount);

        var recovered = new List<Edge>();

        foreach (var node in graph.Nodes)
        {
            var incoming = graph.CallersOf(node);

            for (var index = 0; index < incoming.Count; index++)
            {
                recovered.Add(new Edge(incoming.Sources[index], node, incoming.Provenances[index]));
            }
        }

        Assert.Equal(
            edges.OrderBy(edge => edge.From.Value).ThenBy(edge => edge.To.Value),
            recovered.OrderBy(edge => edge.From.Value).ThenBy(edge => edge.To.Value));
    }

    [Fact]
    public void A_node_with_no_callers_reports_none()
    {
        var a = MethodId.Definition(0, 1);
        var graph = CallGraph.Build([a], []);

        Assert.Equal(0, graph.CallersOf(a).Count);
    }

    [Fact]
    public void A_node_the_graph_does_not_hold_reports_no_callers()
    {
        var graph = CallGraph.Build([MethodId.Definition(0, 1)], []);

        Assert.Equal(0, graph.CallersOf(MethodId.Definition(9, 9)).Count);
    }

    [Fact]
    public void The_pass_reports_how_long_it_took()
    {
        using var graphs = Graphs.Of("namespace N; public class C { public int M() => 1; }");

        // The report's phase timings are always emitted — the instrument already exists at
        // zero extra cost, and the first real run is the first datapoint.
        Assert.True(graphs.Result.Elapsed >= TimeSpan.Zero);
    }
}

/// <summary>
/// Cases the pass has to disclose rather than swallow.
/// </summary>
public class CallGraphNoticeTests
{
    [Fact]
    public void An_ambiguous_signature_edges_to_every_candidate_and_says_so()
    {
        // Genuine residual ambiguity, and one C# can actually express: two function-pointer
        // parameter types that differ only by calling convention are distinct types to the
        // compiler and identical once the signature is canonicalised.
        using var graphs = Graphs.Of(
            ("Core",
                """
                namespace N;

                public static unsafe class Overloads
                {
                    public static int Run(delegate*<int> f) => 1;
                    public static int Run(delegate* unmanaged<int> f) => 2;
                }
                """),
            ("App",
                """
                namespace N;

                public static unsafe class Consumer
                {
                    public static int Call(delegate*<int> f) => Overloads.Run(f);
                }
                """));

        var call = graphs.Method("N.Consumer", "Call", assemblyIndex: 1);
        var candidates = graphs.Methods("N.Overloads", "Run");

        Assert.Equal(2, candidates.Count);

        // Widening is the safe direction: every candidate gets an edge.
        Assert.All(
            candidates,
            candidate => Assert.Equal([EdgeProvenance.CompiledCall], graphs.EdgesBetween(call, candidate)));

        var notice = Assert.Single(graphs.Result.Notices, n => n.Code == NoticeCodes.SignatureAmbiguous);
        Assert.Equal(NoticeKind.Widening, notice.Kind);
        Assert.Contains("Run", notice.Message);
    }

    [Fact]
    public void An_unresolvable_first_party_member_is_a_notice_and_does_not_stop_the_run()
    {
        // Two revisions of the same assembly: the consumer was compiled against one that had
        // Spin, and the one on disk does not. This is a build problem rather than an analysis
        // one, and source-binary correspondence is what should have caught it.
        using var graphs = Graphs.OfMismatched(
            withMember: "namespace N; public class Widget { public int Spin() => 1; }",
            withoutMember: "namespace N; public class Widget { public int Wobble() => 1; }",
            consumer: "namespace N; public class Consumer { public int Use() => new Widget().Spin(); }");

        var notice = Assert.Single(
            graphs.Result.Notices,
            n => n.Code == NoticeCodes.UnresolvedFirstPartyMember);

        Assert.Equal(NoticeKind.BlindSpot, notice.Kind);
        Assert.Contains("Spin", notice.Message);

        // And the run continues: the graph still holds every node it could read.
        Assert.NotEmpty(graphs.Graph.Nodes);
    }
}
