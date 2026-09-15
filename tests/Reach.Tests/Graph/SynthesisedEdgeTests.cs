using Reach.Graph;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Graph;

/// <summary>
/// The two places where control flow is certain but no instruction expresses it. The first
/// assertion here is the one the whole ticket exists for: without containment, every
/// <c>async</c> method body in a solution is unreachable, and it fails silently.
/// </summary>
public class SynthesisedEdgeTests
{
    // ---- Containment -------------------------------------------------------------------------

    [Fact]
    public void A_change_inside_an_async_body_reaches_a_caller_that_awaits_it()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Service
            {
                public async System.Threading.Tasks.Task<int> Work()
                {
                    await System.Threading.Tasks.Task.Yield();
                    return Helper();
                }

                public int Helper() => 1;
            }

            public class Caller
            {
                public async System.Threading.Tasks.Task<int> Use(Service service) => await service.Work();
            }
            """);

        // The body lives in <Work>d__0::MoveNext, which the BCL invokes through
        // AsyncTaskMethodBuilder::Start. Without a containment edge the walk backwards from a
        // change inside it dead-ends in an assembly Reach does not analyse.
        var moveNext = MoveNextIn(graphs, "N.Service", "Work");

        Assert.True(Reaches(graphs, graphs.Method("N.Caller", "Use"), moveNext));
    }

    [Fact]
    public void A_change_inside_an_iterator_body_reaches_a_caller_that_enumerates_it()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Source
            {
                public System.Collections.Generic.IEnumerable<int> Numbers()
                {
                    yield return Seed();
                    yield return 2;
                }

                public int Seed() => 1;
            }

            public class Caller
            {
                public int Total(Source source)
                {
                    var total = 0;

                    foreach (var number in source.Numbers())
                    {
                        total += number;
                    }

                    return total;
                }
            }
            """);

        var moveNext = MoveNextIn(graphs, "N.Source", "Numbers");

        Assert.True(Reaches(graphs, graphs.Method("N.Caller", "Total"), moveNext));
    }

    [Fact]
    public void A_change_inside_a_lambda_reaches_its_enclosing_method_via_the_capture_edge()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Maker
            {
                public System.Func<int> Make()
                {
                    var seed = Seed();
                    return () => seed + 1;
                }

                public int Seed() => 1;
            }
            """);

        var make = graphs.Method("N.Maker", "Make");
        var lambda = graphs.Graph.Nodes.First(node =>
            !node.IsExternal && NameOf(graphs, node).Contains("b__", StringComparison.Ordinal));

        // Asserted so the two mechanisms stay distinguishable: a lambda emits ldftn inside the
        // kernel method, so the capture rule already covers it and containment is not what
        // carries this one.
        Assert.Contains(EdgeProvenance.CompiledCapture, graphs.EdgesBetween(make, lambda));
    }

    [Fact]
    public void A_change_inside_a_local_function_reaches_its_enclosing_method()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Outer
            {
                public int Run()
                {
                    return Helper();
                    int Helper() => 1;
                }
            }
            """);

        var helper = graphs.Graph.Nodes.First(node =>
            !node.IsExternal && NameOf(graphs, node).Contains("g__Helper", StringComparison.Ordinal));

        // An ordinary call, so no rule is needed for it either.
        Assert.Contains(EdgeProvenance.CompiledCall, graphs.EdgesBetween(graphs.Method("N.Outer", "Run"), helper));
    }

    [Fact]
    public void A_display_class_the_attributes_do_not_name_is_reached_by_the_name_fallback()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Maker
            {
                public System.Func<int> Make(int seed) => () => seed + 1;
            }
            """);

        var make = graphs.Method("N.Maker", "Make");

        var displayClassMember = graphs.Graph.Nodes.First(node =>
            !node.IsExternal && NameOf(graphs, node).Contains("DisplayClass", StringComparison.Ordinal));

        // No attribute points at a display class, so the mangled name is the fallback — and it
        // is a fallback, not the primary mechanism.
        Assert.True(Reaches(graphs, make, displayClassMember));
    }

    [Fact]
    public void Containment_edges_carry_synthesised_provenance()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Service
            {
                public async System.Threading.Tasks.Task<int> Work()
                {
                    await System.Threading.Tasks.Task.Yield();
                    return 1;
                }
            }
            """);

        var provenances = graphs.EdgesBetween(
            graphs.Method("N.Service", "Work"),
            MoveNextIn(graphs, "N.Service", "Work"));

        // Invented by Reach, but the control flow they describe is not in doubt — so they are
        // as non-removable as compiled edges, and tagging them widened would put a certain edge
        // inside a future narrowing's safe domain.
        Assert.Contains(EdgeProvenance.Containment, provenances);
        Assert.True(EdgeProvenance.Containment.IsSynthesised());
        Assert.False(EdgeProvenance.Containment.IsWidened());
    }

    [Fact]
    public void A_synthesised_edge_does_not_degrade_a_path_to_widened()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Service
            {
                public async System.Threading.Tasks.Task<int> Work()
                {
                    await System.Threading.Tasks.Task.Yield();
                    return 1;
                }
            }
            """);

        var reached = ReverseWalk.From(graphs.Graph, [MoveNextIn(graphs, "N.Service", "Work")]);

        Assert.Equal(PathClass.Compiled, reached[graphs.Method("N.Service", "Work")]);
    }

    // ---- Type initialization ---------------------------------------------------------------------

    [Fact]
    public void A_lambda_captured_by_a_static_readonly_field_is_reached_through_the_cctor()
    {
        using var graphs = Graphs.Of(
            """
            namespace N;

            public class Registry
            {
                public static readonly System.Func<int, int> Double = value => value * Factor();

                public static int Factor() => 2;
            }

            public class Caller
            {
                public int Use() => Registry.Double(3);
            }
            """);

        var use = graphs.Method("N.Caller", "Use");

        var lambda = graphs.Graph.Nodes.First(node =>
            !node.IsExternal && NameOf(graphs, node).Contains("b__", StringComparison.Ordinal));

        // The static readonly Func compiles to ldftn inside .cctor, which nothing visibly
        // calls — so without the initialization edge the captured lambda is orphaned. Verified
        // rather than hypothetical.
        Assert.True(Reaches(graphs, use, lambda));
    }

    [Theory]
    [InlineData("public int Use() => new Target().Value;")]
    [InlineData("public int Use() => Target.Shared;")]
    [InlineData("public int Use() => Target.Compute();")]
    public void Each_initialization_trigger_produces_a_cctor_edge(string body)
    {
        using var graphs = Graphs.Of(
            $$"""
            namespace N;

            public class Target
            {
                public static readonly int Shared;

                static Target() { Shared = 1; }

                public int Value => 1;

                public static int Compute() => 2;
            }

            public class Caller
            {
                {{body}}
            }
            """);

        Assert.Contains(
            EdgeProvenance.TypeInitialization,
            graphs.EdgesBetween(graphs.Method("N.Caller", "Use"), graphs.Method("N.Target", ".cctor")));
    }

    [Fact]
    public void Type_initialization_edges_carry_synthesised_provenance()
    {
        Assert.True(EdgeProvenance.TypeInitialization.IsSynthesised());
        Assert.False(EdgeProvenance.TypeInitialization.IsWidened());
    }

    // ---- Helpers ------------------------------------------------------------------------------------

    private static MethodId MoveNextIn(Graphs graphs, string declaringType, string kernelMethod)
    {
        var expected = $"{declaringType}+<{kernelMethod}>d__";

        return graphs.Graph.Nodes.First(node =>
            !node.IsExternal
            && NameOf(graphs, node).StartsWith(expected, StringComparison.Ordinal)
            && NameOf(graphs, node).EndsWith("::MoveNext", StringComparison.Ordinal));
    }

    private static string NameOf(Graphs graphs, MethodId method)
    {
        var assembly = graphs.Assemblies[method.Ordinal];
        var reader = assembly.Reader;

        foreach (var handle in reader.MethodDefinitions)
        {
            if (System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(handle) != method.Token)
            {
                continue;
            }

            var definition = reader.GetMethodDefinition(handle);

            return MetadataNames.FullNameOf(reader, definition.GetDeclaringType())
                + "::"
                + reader.GetString(definition.Name);
        }

        return method.ToString();
    }

    private static bool Reaches(Graphs graphs, MethodId from, MethodId to) =>
        ReverseWalk.From(graphs.Graph, [to]).ContainsKey(from);
}
