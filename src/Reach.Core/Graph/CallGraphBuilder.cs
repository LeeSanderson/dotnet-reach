using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Reach.Reporting;

namespace Reach.Graph;

/// <summary>
/// One pass over the analysis scope's assemblies, producing nodes and compiled edges.
/// </summary>
/// <remarks>
/// <para>
/// A node is one <c>MethodDefinition</c>, per assembly instance. Never a source declaration,
/// never a generic instantiation. Accessors are nodes in their own right, because
/// <c>get_Total</c> and <c>op_Equality</c> are ordinary methods in IL and inventing a synthetic
/// property node would make the graph carry a member IL does not have.
/// </para>
/// <para>
/// <strong>Dispatch declarations are nodes.</strong> <c>callvirt IRepo.Save</c> edges to a node
/// for <c>IRepo.Save</c>, and widening edges hang off <em>that</em> node. Edging call sites
/// straight to implementations would smear widening across the graph where it can be neither
/// isolated nor counted.
/// </para>
/// <para>
/// Deliberately not optimised: no concurrency of any kind, no persisted graph, no memory-mapped
/// reads, no pooling, no struct-of-arrays layout, and no string interning beyond the resolution
/// index, which is rebuilt every run.
/// </para>
/// </remarks>
internal sealed class CallGraphBuilder
{
    private readonly List<GraphAssembly> _assemblies = [];
    private readonly List<Edge> _edges = [];
    private readonly List<MethodId> _declared = [];

    /// <summary>
    /// Type name to its <c>.cctor</c>, for the initialization-trigger edges.
    /// </summary>
    private readonly Dictionary<string, MethodId> _classConstructors = new(StringComparer.Ordinal);

    private int _hierarchyConstructions;

    private CallSiteResolver CallSites
    {
        get => field ?? throw new InvalidOperationException("Call sites are resolved once every first-party assembly is known.");
        set;
    }

    /// <summary>
    /// Built once at the start of a run, over the one type hierarchy.
    /// </summary>
    private DispatchAnchors Dispatch
    {
        get => field ?? throw new InvalidOperationException("The type hierarchy is built at the start of a run.");
        set;
    }


    /// <summary>
    /// Builds the graph over <paramref name="firstParty"/>.
    /// </summary>
    /// <param name="firstParty">The analysis scope's assemblies.</param>
    /// <param name="entryEdges">
    /// Synthesised edges whose knowledge the graph does not own: the test lifecycle edges, which
    /// need test recognition. Selection derives them from the same assemblies.
    /// </param>
    internal static CallGraphResult Build(IEnumerable<GraphAssembly> firstParty, IEnumerable<Edge>? entryEdges = null)
    {
        var builder = new CallGraphBuilder();

        builder._edges.AddRange(entryEdges ?? []);

        return builder.Run(firstParty);
    }

    private CallGraphResult Run(IEnumerable<GraphAssembly> firstParty)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();

        _assemblies.AddRange(firstParty);
        CallSites = new CallSiteResolver(_assemblies);

        // Exactly one, built here and passed down. Resolving implementations per call site is
        // accidentally quadratic and will look fine on a sample repository and fail on a
        // client's.
        Dispatch = new DispatchAnchors(new TypeHierarchy(_assemblies));
        _hierarchyConstructions++;

        foreach (var assembly in _assemblies)
        {
            IndexClassConstructors(assembly);
        }

        foreach (var assembly in _assemblies)
        {
            Walk(assembly);

            // Without this, every async method body in the solution is unreachable.
            _edges.AddRange(SynthesisedEdges.Containment(assembly));
        }

        _edges.AddRange(Dispatch.Widen());

        return new CallGraphResult(
            CallGraph.Build(_declared, _edges),
            _assemblies,
            CallSites.Externals,
            CallSites.Notices(),
            started.Elapsed,
            _hierarchyConstructions);
    }

    private void Walk(GraphAssembly assembly)
    {
        var reader = assembly.Reader;

        foreach (var handle in reader.MethodDefinitions)
        {
            var from = MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle));
            _declared.Add(from);

            var method = reader.GetMethodDefinition(handle);

            if (method.RelativeVirtualAddress == 0 || assembly.PEReader is null)
            {
                continue;
            }

            byte[] il;
            IReadOnlyDictionary<int, string> receivers;

            try
            {
                var body = assembly.PEReader.GetMethodBody(method.RelativeVirtualAddress);

                il = body.GetILBytes() ?? [];
                receivers = ReceiverTypes.Infer(assembly, method, body, il);
            }
            catch (BadImageFormatException)
            {
                continue;
            }

            var instructions = ILInstructions.Tokens(il);

            AddCompiledEdges(assembly, from, instructions, receivers);

            _edges.AddRange(SynthesisedEdges.TypeInitialization(
                assembly,
                from,
                instructions,
                type => _classConstructors.TryGetValue(type, out var cctor) ? cctor : null));
        }
    }

    private void AddCompiledEdges(
        GraphAssembly assembly,
        MethodId from,
        IReadOnlyList<TokenInstruction> instructions,
        IReadOnlyDictionary<int, string> receivers)
    {
        foreach (var instruction in instructions)
        {
            var provenance = ProvenanceOf(instruction.OpCode);

            if (provenance is null)
            {
                continue;
            }

            foreach (var to in CallSites.Resolve(assembly, instruction.Token))
            {
                _edges.Add(new Edge(
                    from,
                    Dispatch.Anchor(assembly, instruction, receivers, to),
                    provenance.Value));
            }
        }
    }

    private void IndexClassConstructors(GraphAssembly assembly)
    {
        var reader = assembly.Reader;

        foreach (var handle in reader.TypeDefinitions)
        {
            var name = MetadataNames.FullNameOf(reader, handle);

            var classConstructors = reader.GetTypeDefinition(handle).GetMethods()
                .Where(methodHandle => reader.GetString(reader.GetMethodDefinition(methodHandle).Name) == ".cctor");

            foreach (var methodHandle in classConstructors)
            {
                _classConstructors[name] = MethodId.Definition(
                    assembly.Ordinal,
                    MetadataTokens.GetToken(methodHandle));
            }
        }
    }

    /// <summary>
    /// The two compiled edge kinds, and nothing else.
    /// </summary>
    /// <remarks>
    /// <c>Invoke</c> and <c>calli</c> deliberately create none. Edging every <c>Invoke</c> on a
    /// delegate type to every method ever captured into it <em>looks</em> like widen-when-uncertain
    /// and is not conservatism: <c>Func&lt;T&gt;</c> and <c>Action</c> are structural types shared
    /// by unrelated code, so it connects everything to everything and makes the graph useless
    /// rather than safe. The capture-site rule is far less lossy than it looks, because a test
    /// that can execute the target must have executed the code that created the delegate.
    /// </remarks>
    private static EdgeProvenance? ProvenanceOf(ILOpCode opCode) => opCode switch
    {
        // newobj calls a constructor, and the constructor is a node like any other. Without it
        // a change to a constructor is unreachable from `new Foo()`, and an iterator's
        // `newobj '<It>d__1'::.ctor` — which the design names as a real compiled edge — is not
        // an edge at all.
        ILOpCode.Call or ILOpCode.Callvirt or ILOpCode.Newobj => EdgeProvenance.CompiledCall,

        // From the capturing method to the target. `delegate*` values fall out for free — they
        // are ldftn like any other capture.
        ILOpCode.Ldftn or ILOpCode.Ldvirtftn => EdgeProvenance.CompiledCapture,

        _ => null,
    };
}

/// <summary>
/// The graph, plus everything the pass had to disclose while building it.
/// </summary>
/// <param name="Graph">The nodes and edges.</param>
/// <param name="Assemblies">The assemblies the pass read.</param>
/// <param name="Externals">Members of assemblies outside the scope that edges point at.</param>
/// <param name="Notices">What the pass had to disclose.</param>
/// <param name="Elapsed">How long the pass took.</param>
/// <param name="HierarchyConstructions">
/// How many type hierarchy indexes this run built. One of M1's two committed algorithmic
/// constraints is that it is exactly one, and this is what makes that a test rather than a
/// review comment.
/// </param>
public sealed record CallGraphResult(
    CallGraph Graph,
    IReadOnlyList<GraphAssembly> Assemblies,
    ExternalAnchors Externals,
    IReadOnlyList<Notice> Notices,
    TimeSpan Elapsed,
    int HierarchyConstructions);
