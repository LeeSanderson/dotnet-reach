using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Reach.Reporting;

namespace Reach.Graph;

/// <summary>
/// Turns a call site's token into the methods it can reach: a first-party method by its
/// identity, an external one by an anchor interned only where a first-party type occupies its
/// slot, and nothing where the scope cannot say. Everything it could not resolve cleanly is
/// disclosed in <see cref="Notices"/>.
/// </summary>
internal sealed class CallSiteResolver
{
    /// <summary>
    /// How many members a notice names inline. The full list is always in the notice's data.
    /// </summary>
    private const int NamedInMessage = 20;

    private readonly int _firstPartyCount;
    private readonly Dictionary<string, List<GraphAssembly>> _firstPartyByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _unresolvedFirstParty = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ambiguous = new(StringComparer.Ordinal);

    /// <summary>
    /// External types a first-party type inherits from or implements. An external member is
    /// worth an anchor only when a first-party type occupies the slot — which is what bounds
    /// the interned set by the solution's own shape rather than by the BCL's size.
    /// </summary>
    private readonly HashSet<string> _implementedExternalTypes = new(StringComparer.Ordinal);

    internal CallSiteResolver(IReadOnlyList<GraphAssembly> firstParty)
    {
        _firstPartyCount = firstParty.Count;

        foreach (var assembly in firstParty)
        {
            if (!_firstPartyByName.TryGetValue(assembly.Name, out var instances))
            {
                _firstPartyByName[assembly.Name] = instances = [];
            }

            instances.Add(assembly);
        }

        foreach (var assembly in firstParty)
        {
            CollectExternalSlots(assembly);
        }
    }

    /// <summary>
    /// The members of assemblies outside the scope that resolved call sites point at.
    /// </summary>
    internal ExternalAnchors Externals { get; } = new();

    private int NextOrdinal => _firstPartyCount + Externals.AssemblyCount;

    /// <summary>
    /// The declaring type's canonical name, and the assembly it lives in.
    /// </summary>
    internal static (string? Type, string? Assembly) ParentOf(
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
    /// The methods a call site's token can reach. Several only for the residual ambiguity the
    /// design expects — varargs, <c>modopt</c>/<c>modreq</c>, function-pointer parameters —
    /// where edging to every candidate is the safe direction.
    /// </summary>
    internal IReadOnlyList<MethodId> Resolve(GraphAssembly assembly, int token)
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

    /// <summary>
    /// What resolving had to disclose: call sites naming a first-party member that is not
    /// there, and signatures matching more than one method.
    /// </summary>
    internal IReadOnlyList<Notice> Notices()
    {
        var notices = new List<Notice>();

        if (_unresolvedFirstParty.Count > 0)
        {
            notices.Add(new Notice(
                NoticeCodes.UnresolvedFirstPartyMember,
                NoticeKind.BlindSpot,
                $"{_unresolvedFirstParty.Count} call site(s) name a member of a first-party assembly "
                + "that is not in it, so those calls produced no edge. The assemblies on disk do "
                + "not agree with each other: "
                + string.Join("; ", _unresolvedFirstParty.Order(StringComparer.Ordinal).Take(NamedInMessage)),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["members"] = _unresolvedFirstParty.Order(StringComparer.Ordinal).ToArray(),
                }));
        }

        if (_ambiguous.Count > 0)
        {
            notices.Add(new Notice(
                NoticeCodes.SignatureAmbiguous,
                NoticeKind.Widening,
                $"{_ambiguous.Count} call site(s) name a signature that matches more than one "
                + "method, so every candidate got an edge: "
                + string.Join("; ", _ambiguous.Order(StringComparer.Ordinal).Take(NamedInMessage)),
                new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["members"] = _ambiguous.Order(StringComparer.Ordinal).ToArray(),
                }));
        }

        return notices;
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

            RememberImplemented(reader, type.BaseType);

            foreach (var implementation in type.GetInterfaceImplementations())
            {
                RememberImplemented(reader, reader.GetInterfaceImplementation(implementation).Interface);
            }
        }
    }

    private void RememberImplemented(MetadataReader reader, EntityHandle type)
    {
        if (type.IsNil)
        {
            return;
        }

        var name = type.Kind switch
        {
            HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)type),
            HandleKind.TypeSpecification => MetadataNames.WithoutInstantiation(
                reader.GetTypeSpecification((TypeSpecificationHandle)type)
                    .DecodeSignature(new SignatureNames(), genericContext: null)),
            _ => null,
        };

        if (name is not null)
        {
            _implementedExternalTypes.Add(name);
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
            return _implementedExternalTypes.Contains(declaringType)
                ? [Externals.Intern(assemblyName ?? "?", key, NextOrdinal)]
                : [];
        }

        if (!target.Index.TryGetValue(key, out var candidates) || candidates.Count == 0)
        {
            // First-party but unresolvable: a build problem rather than an analysis one, and
            // source–binary correspondence is what should have caught it. Disclosed, and the
            // run continues.
            _unresolvedFirstParty.Add($"{target.Name}: {key}");
            return [];
        }

        if (candidates.Count > 1)
        {
            _ambiguous.Add($"{target.Name}: {key}");
        }

        return candidates;
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

        if (!_firstPartyByName.TryGetValue(assemblyName, out var candidates))
        {
            return null;
        }

        return candidates.Find(candidate =>
                candidate.Framework is not null
                && from.Framework is not null
                && candidate.Framework.Matches(from.Framework))
            ?? candidates[0];
    }
}
