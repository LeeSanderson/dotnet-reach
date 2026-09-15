using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Reach.Assemblies;
using Reach.Reporting;

namespace Reach.Graph;

/// <summary>The graph, plus everything the pass had to disclose while building it.</summary>
/// <param name="HierarchyConstructions">
/// How many type hierarchy indexes this run built. One of M1's two committed algorithmic
/// constraints is that it is exactly one, and this is what makes that a test rather than a
/// review comment.
/// </param>
internal sealed record CallGraphResult(
    CallGraph Graph,
    IReadOnlyList<GraphAssembly> Assemblies,
    ExternalAnchors Externals,
    IReadOnlyList<Notice> Notices,
    TimeSpan Elapsed,
    int HierarchyConstructions);

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
    private readonly List<GraphAssembly> assemblies = [];
    private readonly Dictionary<string, List<GraphAssembly>> firstPartyByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly ExternalAnchors externals = new();
    private readonly List<Edge> edges = [];
    private readonly List<MethodId> declared = [];
    private readonly List<Notice> notices = [];
    private readonly HashSet<string> unresolvedFirstParty = new(StringComparer.Ordinal);
    private readonly HashSet<string> ambiguous = new(StringComparer.Ordinal);

    /// <summary>
    /// External types a first-party type inherits from or implements. An external member is
    /// worth an anchor only when a first-party type occupies the slot — which is what bounds
    /// the interned set by the solution's own shape rather than by the BCL's size.
    /// </summary>
    private readonly HashSet<string> implementedExternalTypes = new(StringComparer.Ordinal);

    /// <summary>
    /// Dispatch declarations some call site actually reached, and the member key each one
    /// names. Widening hangs off these nodes, never off the call sites — smearing widened edges
    /// across the graph would leave them neither isolable nor countable, which breaks the
    /// measurement.
    /// </summary>
    private readonly Dictionary<MethodId, (string Type, string Member)> dispatchTargets = [];

    private TypeHierarchy hierarchy = null!;

    private int hierarchyConstructions;

    /// <summary>
    /// Ordinals are assigned in a deterministic order — assembly simple name, then target
    /// framework — so a debugging session is reproducible. No ordinal ever reaches the report,
    /// so report determinism does not depend on this.
    /// </summary>
    internal static IReadOnlyList<AssemblyInstance> InOrdinalOrder(IEnumerable<AssemblyInstance> instances) =>
    [
        .. instances
            .OrderBy(instance => instance.Assembly.SimpleName, StringComparer.Ordinal)
            .ThenBy(instance => instance.Expected.TargetFramework, StringComparer.Ordinal)
    ];

    internal static CallGraphResult Build(IEnumerable<GraphAssembly> firstParty)
    {
        var builder = new CallGraphBuilder();

        return builder.Run(firstParty);
    }

    private CallGraphResult Run(IEnumerable<GraphAssembly> firstParty)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();

        foreach (var assembly in firstParty)
        {
            assemblies.Add(assembly);

            if (!firstPartyByName.TryGetValue(assembly.Name, out var instances))
            {
                firstPartyByName[assembly.Name] = instances = [];
            }

            instances.Add(assembly);
        }

        foreach (var assembly in assemblies)
        {
            CollectExternalSlots(assembly);
        }

        // Exactly one, built here and passed down. Resolving implementations per call site is
        // accidentally quadratic and will look fine on a sample repository and fail on a
        // client's.
        hierarchy = new TypeHierarchy(assemblies);
        hierarchyConstructions++;

        foreach (var assembly in assemblies)
        {
            Walk(assembly);
        }

        Widen();
        NoteUnresolved();

        return new CallGraphResult(
            CallGraph.Build(declared, edges),
            assemblies,
            externals,
            notices,
            started.Elapsed,
            hierarchyConstructions);
    }

    /// <summary>
    /// Records the external types first-party types derive from or implement, so that
    /// <c>callvirt object::ToString</c> against a type that overrides it gets an anchor and the
    /// same call against one that does not gets nothing.
    /// </summary>
    private void CollectExternalSlots(GraphAssembly assembly)
    {
        var reader = assembly.Reader;

        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);

            Remember(reader, type.BaseType);

            foreach (var implementation in type.GetInterfaceImplementations())
            {
                Remember(reader, reader.GetInterfaceImplementation(implementation).Interface);
            }
        }
    }

    private void Remember(MetadataReader reader, EntityHandle type)
    {
        if (type.IsNil)
        {
            return;
        }

        switch (type.Kind)
        {
            case HandleKind.TypeReference:
                implementedExternalTypes.Add(MetadataNames.FullNameOf(reader, (TypeReferenceHandle)type));
                break;

            case HandleKind.TypeSpecification:
                var names = new SignatureNames();
                var decoded = reader.GetTypeSpecification((TypeSpecificationHandle)type)
                    .DecodeSignature(names, genericContext: null);

                implementedExternalTypes.Add(MetadataNames.WithoutInstantiation(decoded));
                break;
        }
    }

    private void Walk(GraphAssembly assembly)
    {
        var reader = assembly.Reader;

        foreach (var handle in reader.MethodDefinitions)
        {
            var from = MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle));
            declared.Add(from);

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

            foreach (var instruction in ILInstructions.Tokens(il))
            {
                var provenance = ProvenanceOf(instruction.OpCode);

                if (provenance is null)
                {
                    continue;
                }

                foreach (var to in Resolve(assembly, instruction.Token))
                {
                    edges.Add(new Edge(
                        from,
                        Anchor(assembly, instruction, receivers, to),
                        provenance.Value));
                }
            }
        }
    }

    /// <summary>
    /// Re-anchors a dispatch to the slot as seen from the inferred receiver type, and records
    /// the node so widening can hang off it.
    /// </summary>
    /// <remarks>
    /// This is where the whole ticket earns its place. Taking the instruction's own token would
    /// send every <c>ToString()</c> call site to one <c>object::ToString</c> node, which widens
    /// to every override anywhere — not conservatism, but a graph with no information in it.
    /// </remarks>
    private MethodId Anchor(
        GraphAssembly assembly,
        TokenInstruction instruction,
        IReadOnlyDictionary<int, string> receivers,
        MethodId token)
    {
        // callvirt and ldvirtftn *are* dispatch, whatever the declaring type is — which
        // matters, because the slot is routinely object::ToString or IDisposable::Dispose in an
        // assembly Reach never reads. `call` is included only because a static abstract
        // interface member dispatches with it, and it needs the check: widening an ordinary
        // call would reach a subtype's `new`-shadowed method, which never runs from that site.
        var isDispatch = instruction.OpCode is ILOpCode.Callvirt or ILOpCode.Ldvirtftn;

        if (!isDispatch && instruction.OpCode != ILOpCode.Call)
        {
            return token;
        }

        var member = MemberKeyOf(assembly, instruction.Token);

        if (member is null)
        {
            return token;
        }

        if (!isDispatch && !hierarchy.IsDispatchable(member.Value.Type, member.Value.Member))
        {
            return token;
        }

        if (!receivers.TryGetValue(instruction.Offset, out var receiver))
        {
            Remember(token, member.Value);
            return token;
        }

        var slot = hierarchy.SlotFor(receiver, member.Value.Member);

        if (slot is null || hierarchy.MethodOf(slot, member.Value.Member) is not { } anchored)
        {
            Remember(token, member.Value);
            return token;
        }

        Remember(anchored, (slot, member.Value.Member));

        return anchored;
    }

    private void Remember(MethodId node, (string Type, string Member) member) =>
        dispatchTargets[node] = member;

    /// <summary>The declaring type and member key a call instruction names, as the hierarchy spells them.</summary>
    private (string Type, string Member)? MemberKeyOf(GraphAssembly assembly, int token)
    {
        try
        {
            var reader = assembly.Reader;
            var handle = MetadataTokens.EntityHandle(token);
            var names = new SignatureNames();

            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                    {
                        var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);

                        return (
                            MetadataNames.FullNameOf(reader, method.GetDeclaringType()),
                            MetadataNames.Key(
                                reader.GetString(method.Name),
                                MetadataNames.GenericArityOf(reader, method),
                                method.DecodeSignature(names, genericContext: null)));
                    }

                case HandleKind.MemberReference:
                    {
                        var reference = reader.GetMemberReference((MemberReferenceHandle)handle);

                        if (reference.GetKind() != MemberReferenceKind.Method)
                        {
                            return null;
                        }

                        var signature = reference.DecodeMethodSignature(names, genericContext: null);
                        var (declaringType, _) = ParentOf(reader, reference.Parent, names);

                        return declaringType is null
                            ? null
                            : (declaringType,
                                MetadataNames.Key(
                                    reader.GetString(reference.Name),
                                    signature.GenericParameterCount,
                                    signature));
                    }

                case HandleKind.MethodSpecification:
                    return MemberKeyOf(
                        assembly,
                        MetadataTokens.GetToken(
                            reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method));

                default:
                    return null;
            }
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Edges from every dispatch declaration a call site reached to every implementation it
    /// could run, computed once per node rather than once per call site.
    /// </summary>
    private void Widen()
    {
        foreach (var (node, (type, member)) in dispatchTargets)
        {
            foreach (var implementation in hierarchy.ImplementationsOf(type, member))
            {
                if (implementation != node)
                {
                    // The only speculative class, and therefore the only class any future
                    // narrowing may touch.
                    edges.Add(new Edge(node, implementation, EdgeProvenance.Widened));
                }
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
        ILOpCode.Call or ILOpCode.Callvirt => EdgeProvenance.CompiledCall,

        // From the capturing method to the target. `delegate*` values fall out for free — they
        // are ldftn like any other capture.
        ILOpCode.Ldftn or ILOpCode.Ldvirtftn => EdgeProvenance.CompiledCapture,

        _ => null,
    };

    /// <summary>
    /// Turns a call site's token into the methods it can reach. Returns several only for the
    /// residual ambiguity the design expects — varargs, <c>modopt</c>/<c>modreq</c>,
    /// function-pointer parameters — where edging to every candidate is the safe direction.
    /// </summary>
    private IReadOnlyList<MethodId> Resolve(GraphAssembly assembly, int token)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.MethodDefinition =>
                    [MethodId.Definition(assembly.Ordinal, token)],

                HandleKind.MemberReference =>
                    ResolveReference(assembly, (MemberReferenceHandle)handle),

                // Generic instantiations collapse to one node per definition, so the
                // specification is followed straight through to the method it names.
                HandleKind.MethodSpecification =>
                    Resolve(assembly, MetadataTokens.GetToken(
                        assembly.Reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method)),

                _ => [],
            };
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    private IReadOnlyList<MethodId> ResolveReference(GraphAssembly assembly, MemberReferenceHandle handle)
    {
        var reader = assembly.Reader;
        var reference = reader.GetMemberReference(handle);

        if (reference.GetKind() != MemberReferenceKind.Method)
        {
            return [];
        }

        var names = new SignatureNames();
        var signature = reference.DecodeMethodSignature(names, genericContext: null);
        var (declaringType, assemblyName) = ParentOf(reader, reference.Parent, names);

        if (declaringType is null)
        {
            return [];
        }

        var key = declaringType
            + "::"
            + MetadataNames.Key(reader.GetString(reference.Name), signature.GenericParameterCount, signature);

        var target = TargetFor(assembly, assemblyName);

        if (target is null)
        {
            // Outside the analysis scope: normal and silent. The accepted blind spot, already
            // registered — with an anchor interned only where a first-party type occupies the
            // slot, because that is where widening will need something to hang off.
            return implementedExternalTypes.Contains(declaringType)
                ? [externals.Intern(assemblyName ?? "?", key, NextOrdinal)]
                : [];
        }

        if (!target.Index.TryGetValue(key, out var candidates) || candidates.Count == 0)
        {
            // First-party but unresolvable: a build problem rather than an analysis one, and
            // source–binary correspondence is what should have caught it. Disclosed, and the
            // run continues.
            unresolvedFirstParty.Add($"{target.Name}: {key}");
            return [];
        }

        if (candidates.Count > 1)
        {
            ambiguous.Add($"{target.Name}: {key}");
        }

        return candidates;
    }

    /// <summary>The declaring type's canonical name, and the assembly it lives in.</summary>
    private static (string? Type, string? Assembly) ParentOf(
        MetadataReader reader,
        EntityHandle parent,
        SignatureNames names)
    {
        switch (parent.Kind)
        {
            case HandleKind.TypeReference:
                var reference = (TypeReferenceHandle)parent;
                return (MetadataNames.FullNameOf(reader, reference), MetadataNames.AssemblyOf(reader, reference));

            case HandleKind.TypeDefinition:
                return (MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)parent), null);

            case HandleKind.TypeSpecification:
                // A member reference against a generic instance carries the definition's own
                // signature, so the instantiation is dropped and the definition is what to
                // look the member up under.
                var specification = reader
                    .GetTypeSpecification((TypeSpecificationHandle)parent)
                    .DecodeSignature(names, genericContext: null);

                // The specification names no assembly of its own; the first type handle the
                // decoder reached does.
                var scope = names.FirstType.Kind == HandleKind.TypeReference
                    ? MetadataNames.AssemblyOf(reader, (TypeReferenceHandle)names.FirstType)
                    : null;

                return (MetadataNames.WithoutInstantiation(specification), scope);

            default:
                return (null, null);
        }
    }

    /// <summary>
    /// The first-party instance a reference resolves into. A multi-targeted project contributes
    /// several, and a <c>net8.0</c> assembly references the <c>net8.0</c> build — so the
    /// calling instance's own framework is the tie-breaker.
    /// </summary>
    private GraphAssembly? TargetFor(GraphAssembly from, string? assemblyName)
    {
        if (assemblyName is null)
        {
            return from;
        }

        if (!firstPartyByName.TryGetValue(assemblyName, out var candidates))
        {
            return null;
        }

        return candidates.FirstOrDefault(candidate =>
                candidate.Framework is not null
                && from.Framework is not null
                && candidate.Framework.Matches(from.Framework))
            ?? candidates[0];
    }

    private int NextOrdinal => assemblies.Count + externals.AssemblyCount;

    private void NoteUnresolved()
    {
        if (unresolvedFirstParty.Count > 0)
        {
            notices.Add(new Notice(
                NoticeCodes.UnresolvedFirstPartyMember,
                NoticeKind.BlindSpot,
                $"{unresolvedFirstParty.Count} call site(s) name a member of a first-party assembly "
                + "that is not in it, so those calls produced no edge. The assemblies on disk do "
                + "not agree with each other: "
                + string.Join("; ", unresolvedFirstParty.Order(StringComparer.Ordinal).Take(20)),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["members"] = unresolvedFirstParty.Order(StringComparer.Ordinal).ToArray(),
                }));
        }

        if (ambiguous.Count > 0)
        {
            notices.Add(new Notice(
                NoticeCodes.SignatureAmbiguous,
                NoticeKind.Widening,
                $"{ambiguous.Count} call site(s) name a signature that matches more than one "
                + "method, so every candidate got an edge: "
                + string.Join("; ", ambiguous.Order(StringComparer.Ordinal).Take(20)),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["members"] = ambiguous.Order(StringComparer.Ordinal).ToArray(),
                }));
        }
    }
}
