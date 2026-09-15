using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Reach.Graph;

/// <summary>
/// The two places where control flow is certain but no instruction expresses it.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Containment is not optional.</strong> <c>async Task&lt;int&gt; Aw()</c> reaches its
/// own body only through <c>AsyncTaskMethodBuilder&lt;int&gt;::Start</c> — <c>MoveNext</c> is
/// invoked from inside the BCL — so a walk backwards from a change inside any <c>async</c>
/// method body dead-ends in an assembly that is not first-party and is not analysed. That is
/// under-selection on ordinary modern C#, not an edge case, and it fails silently.
/// </para>
/// <para>
/// Both kinds carry <strong>synthesised</strong> provenance. They are invented by Reach, but
/// the control flow they describe is not in doubt, so they are as non-removable as compiled
/// edges. Tagging them <c>widened</c> would put a certain edge inside a future narrowing's safe
/// domain.
/// </para>
/// </remarks>
internal static class SynthesisedEdges
{
    private const string StateMachineNamespace = "System.Runtime.CompilerServices";

    private static readonly string[] StateMachineAttributes =
    [
        "AsyncStateMachineAttribute",
        "IteratorStateMachineAttribute",
        "AsyncIteratorStateMachineAttribute",
    ];

    /// <summary>
    /// Edges from each kernel method to the members of the type that carries its body.
    /// </summary>
    /// <remarks>
    /// Exact rather than heuristic: <c>[AsyncStateMachine(typeof('&lt;Aw&gt;d__0'))]</c> names
    /// the generated type directly. The name-mangling convention is the fallback for closures
    /// the attributes do not cover, never the primary mechanism.
    /// </remarks>
    internal static IEnumerable<Edge> Containment(GraphAssembly assembly)
    {
        var reader = assembly.Reader;
        var membersOf = MembersByType(assembly);
        var attributed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var handle in reader.MethodDefinitions)
        {
            var kernel = MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle));
            var method = reader.GetMethodDefinition(handle);

            foreach (var generated in StateMachinesOf(reader, method))
            {
                attributed.Add(generated);

                foreach (var member in membersOf.GetValueOrDefault(generated) ?? [])
                {
                    yield return new Edge(kernel, member, EdgeProvenance.Containment);
                }
            }
        }

        // The fallback, for the closures no attribute points at. Two spellings carry the kernel
        // name, and both are needed: a state machine puts it in the *type* name
        // (`<Work>d__0`), while a display class puts only an ordinal there
        // (`<>c__DisplayClass0_0`) and puts the name on its *members* (`<Make>b__0`).
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            var declaringType = MetadataNames.FullNameOf(reader, method.GetDeclaringType());

            if (attributed.Contains(declaringType))
            {
                continue;
            }

            var kernelName = KernelNameIn(reader.GetString(method.Name)) ?? KernelNameIn(declaringType);

            if (kernelName is null)
            {
                continue;
            }

            var generated = MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle));

            // A generated member's kernel lives on the type that declared the closure, which is
            // the display class's own declaring type.
            foreach (var kernel in MethodsNamed(assembly, DeclaringTypeOf(declaringType), kernelName))
            {
                if (kernel != generated)
                {
                    yield return new Edge(kernel, generated, EdgeProvenance.Containment);
                }
            }
        }
    }

    /// <summary>
    /// Edges from every method that triggers a type's initialization to that type's
    /// <c>.cctor</c>.
    /// </summary>
    /// <remarks>
    /// Cheap, because the trigger instructions are already being decoded to find call edges.
    /// And it closes a real hole rather than a hypothetical one: a
    /// <c>static readonly Func&lt;int,int&gt;</c> field compiles to <c>ldftn</c> inside
    /// <c>.cctor</c>, which nothing visibly calls, so without this edge the captured lambda is
    /// orphaned.
    /// </remarks>
    internal static IEnumerable<Edge> TypeInitialization(
        GraphAssembly assembly,
        MethodId from,
        IEnumerable<TokenInstruction> instructions,
        Func<string, MethodId?> classConstructorOf)
    {
        var triggered = new HashSet<string>(StringComparer.Ordinal);

        foreach (var instruction in instructions)
        {
            var type = instruction.OpCode switch
            {
                ILOpCode.Newobj or ILOpCode.Call or ILOpCode.Callvirt or ILOpCode.Ldftn =>
                    DeclaringTypeOfMember(assembly, instruction.Token),

                ILOpCode.Ldsfld or ILOpCode.Ldsflda or ILOpCode.Stsfld =>
                    DeclaringTypeOfField(assembly, instruction.Token),

                _ => null,
            };

            if (type is null || !triggered.Add(type))
            {
                continue;
            }

            if (classConstructorOf(type) is { } cctor && cctor != from)
            {
                yield return new Edge(from, cctor, EdgeProvenance.TypeInitialization);
            }
        }
    }

    /// <summary>The generated types an attribute on <paramref name="method"/> names.</summary>
    private static IEnumerable<string> StateMachinesOf(MetadataReader reader, MethodDefinition method)
    {
        foreach (var handle in method.GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);

            if (!IsStateMachineAttribute(reader, attribute))
            {
                continue;
            }

            var name = StateMachineTypeName(reader, attribute);

            if (name is not null)
            {
                yield return name;
            }
        }
    }

    private static bool IsStateMachineAttribute(MetadataReader reader, CustomAttribute attribute)
    {
        var parent = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference =>
                reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition =>
                reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default,
        };

        if (parent.IsNil || parent.Kind != HandleKind.TypeReference)
        {
            return false;
        }

        var reference = reader.GetTypeReference((TypeReferenceHandle)parent);

        return reader.GetString(reference.Namespace) == StateMachineNamespace
            && StateMachineAttributes.Contains(reader.GetString(reference.Name), StringComparer.Ordinal);
    }

    /// <summary>
    /// Decodes the single <c>Type</c> argument. A serialised type name arrives as
    /// <c>Namespace.Outer+&lt;M&gt;d__0</c> with an optional assembly-qualified tail.
    /// </summary>
    private static string? StateMachineTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        try
        {
            var blob = reader.GetBlobReader(attribute.Value);

            if (blob.Length < 2 || blob.ReadUInt16() != 1)
            {
                return null;
            }

            var serialised = blob.ReadSerializedString();

            return serialised?.Split(',')[0];
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static Dictionary<string, List<MethodId>> MembersByType(GraphAssembly assembly)
    {
        var reader = assembly.Reader;
        var members = new Dictionary<string, List<MethodId>>(StringComparer.Ordinal);

        foreach (var handle in reader.TypeDefinitions)
        {
            var name = MetadataNames.FullNameOf(reader, handle);
            var methods = new List<MethodId>();

            foreach (var methodHandle in reader.GetTypeDefinition(handle).GetMethods())
            {
                methods.Add(MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(methodHandle)));
            }

            if (methods.Count > 0)
            {
                members[name] = methods;
            }
        }

        return members;
    }

    /// <summary>
    /// The method a compiler-generated name belongs to: <c>&lt;Submit&gt;d__3</c>,
    /// <c>&lt;Submit&gt;b__0</c> and <c>&lt;Submit&gt;g__Local|0_1</c> all name <c>Submit</c>.
    /// Null for <c>&lt;&gt;c</c> and <c>&lt;&gt;c__DisplayClass0_0</c>, whose angle brackets are
    /// empty — those carry only an ordinal, and their members carry the name instead.
    /// </summary>
    private static string? KernelNameIn(string name)
    {
        var leaf = name.Split('+')[^1];
        var close = leaf.IndexOf('>', StringComparison.Ordinal);

        if (!leaf.StartsWith('<') || close <= 1)
        {
            return null;
        }

        return leaf[1..close];
    }

    private static string DeclaringTypeOf(string nestedTypeName)
    {
        var last = nestedTypeName.LastIndexOf('+');

        return last < 0 ? nestedTypeName : nestedTypeName[..last];
    }

    private static IEnumerable<MethodId> MethodsNamed(GraphAssembly assembly, string typeName, string methodName)
    {
        var reader = assembly.Reader;

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);

            if (reader.GetString(method.Name) == methodName
                && MetadataNames.FullNameOf(reader, method.GetDeclaringType()) == typeName)
            {
                yield return MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle));
            }
        }
    }

    private static string? DeclaringTypeOfMember(GraphAssembly assembly, int token)
    {
        try
        {
            var reader = assembly.Reader;
            var handle = MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.MethodDefinition => MetadataNames.FullNameOf(
                    reader,
                    reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType()),
                HandleKind.MemberReference => TypeNameOf(
                    reader,
                    reader.GetMemberReference((MemberReferenceHandle)handle).Parent),
                HandleKind.MethodSpecification => DeclaringTypeOfMember(
                    assembly,
                    MetadataTokens.GetToken(
                        reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method)),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string? DeclaringTypeOfField(GraphAssembly assembly, int token)
    {
        try
        {
            var reader = assembly.Reader;
            var handle = MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.FieldDefinition => MetadataNames.FullNameOf(
                    reader,
                    reader.GetFieldDefinition((FieldDefinitionHandle)handle).GetDeclaringType()),
                HandleKind.MemberReference => TypeNameOf(
                    reader,
                    reader.GetMemberReference((MemberReferenceHandle)handle).Parent),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string? TypeNameOf(MetadataReader reader, EntityHandle handle) => handle.IsNil ? null : handle.Kind switch
    {
        HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)handle),
        HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)handle),
        HandleKind.TypeSpecification => MetadataNames.WithoutInstantiation(
            reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                .DecodeSignature(new SignatureNames(), genericContext: null)),
        _ => null,
    };
}
