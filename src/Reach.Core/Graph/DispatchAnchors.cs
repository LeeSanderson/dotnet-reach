using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Reach.Graph;

/// <summary>
/// Anchors each dispatch site to the slot as seen from its inferred receiver type, remembers
/// every dispatch declaration a call site reached, and widens from those declarations to every
/// implementation they could run.
/// </summary>
/// <remarks>
/// Widening hangs off the dispatch declarations, never off the call sites — smearing widened
/// edges across the graph would leave them neither isolable nor countable, which breaks the
/// measurement.
/// </remarks>
internal sealed class DispatchAnchors(TypeHierarchy hierarchy)
{
    /// <summary>
    /// Dispatch declarations some call site actually reached, and the member key each one names.
    /// </summary>
    private readonly Dictionary<MethodId, DispatchMember> _dispatchTargets = [];

    /// <summary>
    /// Re-anchors a dispatch to the slot as seen from the inferred receiver type, and records
    /// the node so widening can hang off it.
    /// </summary>
    /// <remarks>
    /// This is where the whole ticket earns its place. Taking the instruction's own token would
    /// send every <c>ToString()</c> call site to one <c>object::ToString</c> node, which widens
    /// to every override anywhere — not conservatism, but a graph with no information in it.
    /// </remarks>
    internal MethodId Anchor(
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

        if (MemberOf(assembly, instruction.Token) is not { } member)
        {
            return token;
        }

        if (!isDispatch && !hierarchy.IsDispatchable(member.Type, member.Member))
        {
            return token;
        }

        if (!receivers.TryGetValue(instruction.Offset, out var receiver))
        {
            Remember(token, member);
            return token;
        }

        var slot = hierarchy.SlotFor(receiver, member.Member);

        if (slot is null || hierarchy.MethodOf(slot, member.Member) is not { } anchored)
        {
            Remember(token, member);
            return token;
        }

        Remember(anchored, member with { Type = slot });

        return anchored;
    }

    /// <summary>
    /// Edges from every dispatch declaration a call site reached to every implementation it
    /// could run, computed once per node rather than once per call site.
    /// </summary>
    internal IEnumerable<Edge> Widen()
    {
        foreach (var (node, (type, member)) in _dispatchTargets)
        {
            var implementations = hierarchy.ImplementationsOf(type, member)
                .Where(implementation => implementation != node);

            foreach (var implementation in implementations)
            {
                // The only speculative class, and therefore the only class any future
                // narrowing may touch.
                yield return new Edge(node, implementation, EdgeProvenance.Widened);
            }
        }
    }

    /// <summary>
    /// The declaring type and member key a call instruction names, as the hierarchy spells them.
    /// </summary>
    private static DispatchMember? MemberOf(GraphAssembly assembly, int token)
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

                    return new DispatchMember(
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
                    var (declaringType, _) = MetadataNames.ParentOf(reader, reference.Parent, names);

                    return declaringType is null
                        ? null
                        : new DispatchMember(
                            declaringType,
                            MetadataNames.Key(
                                reader.GetString(reference.Name),
                                signature.GenericParameterCount,
                                signature));
                }

                case HandleKind.MethodSpecification:
                    return MemberOf(
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

    private void Remember(MethodId node, DispatchMember member) =>
        _dispatchTargets[node] = member;

    /// <summary>
    /// A member as the type hierarchy spells it.
    /// </summary>
    /// <param name="Type">The full metadata name of the type that declares the slot.</param>
    /// <param name="Member">The member key within that type.</param>
    private sealed record DispatchMember(string Type, string Member);
}
