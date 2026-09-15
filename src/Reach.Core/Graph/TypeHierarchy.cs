using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Reach.Graph;

/// <summary>
/// Which types derive from which, and which methods implement which slots.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Built in exactly one pass, once per run.</strong> Resolving implementations per call
/// site is accidentally quadratic and <em>will look fine on a sample repository and fail on a
/// client's</em>. This is one of the two algorithmic constraints M1 asserts by test rather than
/// by review — <c>CallGraphResult.HierarchyConstructions</c> is the instrument, counted per
/// run rather than per process so the assertion survives a parallel test suite.
/// </para>
/// </remarks>
internal sealed class TypeHierarchy
{
    private readonly Dictionary<string, TypeInfo> types = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> subtypes = new(StringComparer.Ordinal);

    internal TypeHierarchy(IEnumerable<GraphAssembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            Read(assembly);
        }

        foreach (var (name, type) in types)
        {
            foreach (var parent in type.Parents)
            {
                Bucket(subtypes, parent).Add(name);
            }
        }
    }

    /// <summary>
    /// The type that declares <paramref name="memberKey"/> as seen from
    /// <paramref name="inferredType"/>: itself if it declares one, otherwise the nearest
    /// ancestor that does.
    /// </summary>
    /// <remarks>
    /// This is what turns the inferred receiver type into the node widening hangs off. It is
    /// not narrowing: a variable of static type <c>Sq</c> cannot hold a non-<c>Sq</c> in
    /// verifiable IL, so the bound comes from the type system rather than from inference about
    /// runtime behaviour, and no edge that could run is removed.
    /// </remarks>
    internal string? SlotFor(string inferredType, string memberKey)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>([inferredType]);

        while (pending.TryDequeue(out var name))
        {
            if (!seen.Add(name) || !types.TryGetValue(name, out var type))
            {
                continue;
            }

            if (type.Methods.ContainsKey(memberKey))
            {
                return name;
            }

            foreach (var parent in type.Parents)
            {
                pending.Enqueue(parent);
            }
        }

        return null;
    }

    internal MethodId? MethodOf(string typeName, string memberKey) =>
        types.TryGetValue(typeName, out var type) && type.Methods.TryGetValue(memberKey, out var method)
            ? method
            : null;

    /// <summary>
    /// Whether this member is a slot something else can occupy: an interface member, or a
    /// virtual or abstract method.
    /// </summary>
    /// <remarks>
    /// A static abstract interface member dispatches with <c>call</c> rather than
    /// <c>callvirt</c>, so the opcode cannot be the test — and widening from an ordinary
    /// <c>call</c> would reach a subtype's <c>new</c>-shadowed method, which never runs from
    /// that site. This is what keeps both true at once.
    /// </remarks>
    internal bool IsDispatchable(string typeName, string memberKey)
    {
        if (!types.TryGetValue(typeName, out var type))
        {
            return false;
        }

        return type.IsInterface || type.Virtual.Contains(memberKey);
    }

    internal bool Declares(string typeName) => types.ContainsKey(typeName);

    /// <summary>
    /// Every implementation or override of <paramref name="declaringType"/>'s
    /// <paramref name="memberKey"/>, anywhere at or below it.
    /// </summary>
    /// <remarks>
    /// <c>MethodImpl</c> is authoritative, because an explicit interface implementation is
    /// deliberately <em>not</em> name-matchable; name and signature matching is the fallback
    /// for implicit implementations. A static abstract interface member resolves the same way —
    /// the implementation is selected by the generic instantiation at the call site, which
    /// method identity discards, so every implementer is the only available answer.
    /// </remarks>
    internal IReadOnlyList<MethodId> ImplementationsOf(string declaringType, string memberKey)
    {
        var found = new List<MethodId>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>(subtypes.GetValueOrDefault(declaringType) ?? []);

        while (pending.TryDequeue(out var name))
        {
            if (!seen.Add(name) || !types.TryGetValue(name, out var type))
            {
                continue;
            }

            if (type.MethodImpls.TryGetValue(declaringType + "::" + memberKey, out var explicitly))
            {
                found.Add(explicitly);
            }
            else if (type.Methods.TryGetValue(memberKey, out var implicitly))
            {
                found.Add(implicitly);
            }

            foreach (var child in subtypes.GetValueOrDefault(name) ?? [])
            {
                pending.Enqueue(child);
            }
        }

        return found;
    }

    private void Read(GraphAssembly assembly)
    {
        var reader = assembly.Reader;
        var names = new SignatureNames();

        foreach (var handle in reader.TypeDefinitions)
        {
            var definition = reader.GetTypeDefinition(handle);
            var name = MetadataNames.FullNameOf(reader, handle);

            var parents = new List<string>();

            if (NameOf(reader, definition.BaseType) is { } baseType)
            {
                parents.Add(baseType);
            }

            foreach (var implementation in definition.GetInterfaceImplementations())
            {
                if (NameOf(reader, reader.GetInterfaceImplementation(implementation).Interface) is { } @interface)
                {
                    parents.Add(@interface);
                }
            }

            var methods = new Dictionary<string, MethodId>(StringComparer.Ordinal);
            var virtuals = new HashSet<string>(StringComparer.Ordinal);

            foreach (var methodHandle in definition.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                var key = KeyOf(reader, method, names);

                if (key is null)
                {
                    continue;
                }

                methods[key] = MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(methodHandle));

                if ((method.Attributes & (MethodAttributes.Virtual | MethodAttributes.Abstract)) != 0)
                {
                    virtuals.Add(key);
                }
            }

            var implementations = new Dictionary<string, MethodId>(StringComparer.Ordinal);

            foreach (var implHandle in definition.GetMethodImplementations())
            {
                var impl = reader.GetMethodImplementation(implHandle);
                var declaration = DeclarationOf(reader, impl.MethodDeclaration, names);

                if (declaration is not null && impl.MethodBody.Kind == HandleKind.MethodDefinition)
                {
                    implementations[declaration] = MethodId.Definition(
                        assembly.Ordinal,
                        MetadataTokens.GetToken(impl.MethodBody));
                }
            }

            types[name] = new TypeInfo(
                parents,
                methods,
                implementations,
                virtuals,
                (definition.Attributes & TypeAttributes.Interface) != 0);
        }
    }

    /// <summary>The <c>Type::member</c> spelling a <c>MethodImpl</c>'s declaration half resolves to.</summary>
    private static string? DeclarationOf(MetadataReader reader, EntityHandle handle, SignatureNames names)
    {
        switch (handle.Kind)
        {
            case HandleKind.MemberReference:
                var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                var parent = reference.Parent;

                var declaringType = parent.Kind switch
                {
                    HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)parent),
                    HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)parent),
                    HandleKind.TypeSpecification => MetadataNames.WithoutInstantiation(
                        reader.GetTypeSpecification((TypeSpecificationHandle)parent)
                            .DecodeSignature(new SignatureNames(), genericContext: null)),
                    _ => null,
                };

                if (declaringType is null)
                {
                    return null;
                }

                var signature = reference.DecodeMethodSignature(names, genericContext: null);

                return declaringType
                    + "::"
                    + MetadataNames.Key(
                        reader.GetString(reference.Name),
                        signature.GenericParameterCount,
                        signature);

            case HandleKind.MethodDefinition:
                var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                var key = KeyOf(reader, method, names);

                return key is null
                    ? null
                    : MetadataNames.FullNameOf(reader, method.GetDeclaringType()) + "::" + key;

            default:
                return null;
        }
    }

    private static string? KeyOf(MetadataReader reader, MethodDefinition method, SignatureNames names)
    {
        try
        {
            return MetadataNames.Key(
                reader.GetString(method.Name),
                MetadataNames.GenericArityOf(reader, method),
                method.DecodeSignature(names, genericContext: null));
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// <c>&lt;Module&gt;</c> has no base type, and a nil handle still reports a kind — so the
    /// emptiness check comes first or the row lookup reads off the end of the table.
    /// </summary>
    private static string? NameOf(MetadataReader reader, EntityHandle handle) => handle.IsNil ? null : handle.Kind switch
    {
        HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)handle),
        HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)handle),
        HandleKind.TypeSpecification => MetadataNames.WithoutInstantiation(
            reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                .DecodeSignature(new SignatureNames(), genericContext: null)),
        _ => null,
    };

    private static List<T> Bucket<T>(Dictionary<string, List<T>> index, string key)
    {
        if (!index.TryGetValue(key, out var bucket))
        {
            index[key] = bucket = [];
        }

        return bucket;
    }

    /// <param name="Parents">The base type and every directly implemented interface.</param>
    private sealed record TypeInfo(
        IReadOnlyList<string> Parents,
        Dictionary<string, MethodId> Methods,
        Dictionary<string, MethodId> MethodImpls,
        HashSet<string> Virtual,
        bool IsInterface);
}
