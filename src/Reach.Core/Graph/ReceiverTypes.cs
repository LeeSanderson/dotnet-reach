using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace Reach.Graph;

/// <summary>
/// Recovers the static type a dispatch site can be shown to hold, by a single-pass abstract
/// interpretation of the evaluation stack tracking type names only.
/// </summary>
/// <remarks>
/// <para>
/// A future reader will reasonably ask why there is an abstract interpreter inside what should
/// be a metadata reader. This is why: <strong>Roslyn emits <c>callvirt</c> against the
/// slot-defining declaration</strong>, not the most-derived statically-known type. Verified by
/// disassembly rather than assumed — <c>x.ToString()</c> where <c>x</c> is statically a type
/// that declares an override still emits <c>callvirt System.Object::ToString()</c>, and
/// <c>using var r = new Res()</c> on a <em>sealed</em> class implementing <c>IDisposable</c>
/// emits <c>callvirt System.IDisposable::Dispose()</c> rather than a direct call.
/// </para>
/// <para>
/// Taking the token at face value makes fan-out maximal: every <c>ToString()</c> call site
/// edges to one <c>object::ToString</c> node, which widens to every override anywhere, so
/// changing any one override reverse-reaches almost the entire suite. That is not conservatism,
/// it is a graph with no information in it.
/// </para>
/// <para>
/// <strong>The stack is cleared at every branch target.</strong> A receiver pushed across a
/// branch boundary therefore reads as unknown and falls to the token — which is exactly the
/// stack-merge residue the design accepts: a ternary compiling to branches pushing different
/// types keeps full fan-out at that one site. Errs over.
/// </para>
/// </remarks>
internal static class ReceiverTypes
{
    private const byte TwoBytePrefix = 0xFE;
    private const ushort TwoByteBase = TwoBytePrefix << 8;

    /// <summary>
    /// The per-opcode tables hold the one-byte opcodes, then the two-byte ones by second byte.
    /// </summary>
    private const int OneByteOpCodes = byte.MaxValue + 1;

    private static readonly int[] Pops = new int[512];
    private static readonly int[] Pushes = new int[512];

    private static readonly HashSet<StackBehaviour> SlotlessBehaviours =
        [StackBehaviour.Pop0, StackBehaviour.Push0, StackBehaviour.Varpop, StackBehaviour.Varpush];

    /// <summary>
    /// Every instruction whose operand is a branch target: the conditional and unconditional
    /// branches and <c>leave</c>, long and short. Read from <see cref="OpCodes"/>, like the
    /// other tables here, rather than typed out.
    /// </summary>
    private static readonly HashSet<ILOpCode> Branches =
    [
        .. typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetValue(null))
            .OfType<OpCode>()
            .Where(opCode => opCode.OperandType is OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget)
            .Select(opCode => (ILOpCode)opCode.Value),
    ];

    static ReceiverTypes()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var index = Index((ushort)opCode.Value);

            Pops[index] = Count(opCode.StackBehaviourPop);
            Pushes[index] = Count(opCode.StackBehaviourPush);
        }
    }

    /// <summary>
    /// The inferred receiver type at each call instruction's offset, or absent where the ladder
    /// falls through to the token.
    /// </summary>
    internal static IReadOnlyDictionary<int, string> Infer(
        GraphAssembly assembly,
        MethodDefinition method,
        MethodBodyBlock body,
        ReadOnlySpan<byte> il)
    {
        var reader = assembly.Reader;
        var names = new SignatureNames();

        var arguments = ArgumentTypes(reader, method, names);
        var locals = LocalTypes(reader, body, names);
        var boundaries = Boundaries(il, body);

        var inferred = new Dictionary<int, string>();
        var stack = new List<string?>();
        var offset = 0;

        // An instruction Read cannot decode is a nop with a negative next offset: the walk ends.
        while (offset >= 0 && offset < il.Length)
        {
            if (boundaries.Contains(offset))
            {
                // Everything before a branch target reached it from somewhere this pass did not
                // follow, so nothing on the stack can be trusted across it.
                stack.Clear();
            }

            var (opCode, operand, next) = Read(il, offset);

            Apply(reader, names, arguments, locals, stack, inferred, offset, opCode, operand);

            offset = next;
        }

        return inferred;
    }

    private static void Apply(
        MetadataReader reader,
        SignatureNames names,
        IReadOnlyList<string?> arguments,
        IReadOnlyList<string?> locals,
        List<string?> stack,
        Dictionary<int, string> inferred,
        int offset,
        ILOpCode opCode,
        int operand)
    {
        switch (opCode)
        {
            // Rung 1: an exact type.
            case ILOpCode.Newobj:
                Pop(stack, ArgumentCount(reader, operand, names));
                stack.Add(DeclaringTypeOf(reader, operand, names));
                return;

            case ILOpCode.Castclass:
            case ILOpCode.Isinst:
            case ILOpCode.Unbox_any:
            case ILOpCode.Box:
                Pop(stack, 1);
                stack.Add(TypeNameOf(reader, operand, names));
                return;

            // Rung 2: a signature type.
            // The short forms are numbered consecutively, so the slot is the distance from _0.
            case ILOpCode.Ldarg_0:
            case ILOpCode.Ldarg_1:
            case ILOpCode.Ldarg_2:
            case ILOpCode.Ldarg_3:
                stack.Add(At(arguments, opCode - ILOpCode.Ldarg_0));
                return;
            case ILOpCode.Ldarg:
            case ILOpCode.Ldarg_s:
                stack.Add(At(arguments, operand));
                return;

            case ILOpCode.Ldloc_0:
            case ILOpCode.Ldloc_1:
            case ILOpCode.Ldloc_2:
            case ILOpCode.Ldloc_3:
                stack.Add(At(locals, opCode - ILOpCode.Ldloc_0));
                return;
            case ILOpCode.Ldloc:
            case ILOpCode.Ldloc_s:
                stack.Add(At(locals, operand));
                return;

            case ILOpCode.Ldsfld:
                stack.Add(FieldTypeOf(reader, operand, names));
                return;

            case ILOpCode.Ldfld:
                Pop(stack, 1);
                stack.Add(FieldTypeOf(reader, operand, names));
                return;

            case ILOpCode.Dup:
                stack.Add(stack.Count > 0 ? stack[^1] : null);
                return;

            case ILOpCode.Call:
            case ILOpCode.Callvirt:
            {
                var (parameters, isInstance, returns) = CallShape(reader, operand, names);
                var receiver = Peek(stack, parameters);

                if (isInstance && receiver is not null)
                {
                    inferred[offset] = receiver;
                }

                Pop(stack, parameters + (isInstance ? 1 : 0));

                if (returns is not null)
                {
                    stack.Add(returns);
                }

                return;
            }

            case ILOpCode.Ldftn:
            case ILOpCode.Ldvirtftn:
            {
                // ldvirtftn consumes its receiver; ldftn does not.
                if (opCode == ILOpCode.Ldvirtftn)
                {
                    if (Peek(stack, 0) is { } receiver)
                    {
                        inferred[offset] = receiver;
                    }

                    Pop(stack, 1);
                }

                stack.Add(null);
                return;
            }

            default:
            {
                var index = Index((ushort)opCode);

                Pop(stack, index < Pops.Length ? Pops[index] : 0);

                for (var push = 0; push < (index < Pushes.Length ? Pushes[index] : 0); push++)
                {
                    stack.Add(null);
                }

                return;
            }
        }
    }

    /// <summary>
    /// Offsets a branch or a handler can arrive at, where the stack stops being knowable.
    /// </summary>
    private static HashSet<int> Boundaries(ReadOnlySpan<byte> il, MethodBodyBlock body)
    {
        var boundaries = new HashSet<int>();
        var offset = 0;

        // An instruction Read cannot decode is a nop with a negative next offset: the walk ends.
        while (offset >= 0 && offset < il.Length)
        {
            var (opCode, operand, next) = Read(il, offset);

            if (IsBranch(opCode))
            {
                boundaries.Add(next + operand);
            }
            else if (opCode == ILOpCode.Switch)
            {
                // One opcode byte, a four-byte count, then that many four-byte targets.
                var targets = offset + 1 + sizeof(int);
                var count = BitConverter.ToInt32(il[(offset + 1)..targets]);

                for (var index = 0; index < count; index++)
                {
                    var target = targets + (index * sizeof(int));
                    boundaries.Add(next + BitConverter.ToInt32(il[target..(target + sizeof(int))]));
                }
            }
            else
            {
                // Anything else falls through to the next instruction, which is no boundary.
            }

            offset = next;
        }

        foreach (var region in body.ExceptionRegions)
        {
            boundaries.Add(region.TryOffset);
            boundaries.Add(region.HandlerOffset);

            if (region.FilterOffset >= 0)
            {
                boundaries.Add(region.FilterOffset);
            }
        }

        return boundaries;
    }

    private static bool IsBranch(ILOpCode opCode) => Branches.Contains(opCode);

    /// <summary>
    /// Decodes one instruction: its opcode, its operand as an integer, and where the next one starts.
    /// One it cannot decode reads as a <c>nop</c> with a negative next offset.
    /// </summary>
    private static (ILOpCode OpCode, int Operand, int Next) Read(ReadOnlySpan<byte> il, int offset)
    {
        var first = il[offset++];
        ILOpCode opCode;

        if (first == TwoBytePrefix)
        {
            if (offset >= il.Length)
            {
                return (ILOpCode.Nop, 0, -1);
            }

            opCode = (ILOpCode)(TwoByteBase | il[offset++]);
        }
        else
        {
            opCode = (ILOpCode)first;
        }

        var size = OperandSize(opCode, il, offset);

        if (size < 0 || offset + size > il.Length)
        {
            return (ILOpCode.Nop, 0, -1);
        }

        var operand = size switch
        {
            1 => ShortOperand(opCode, il[offset]),
            2 => BitConverter.ToInt16(il[offset..(offset + 2)]),
            4 => BitConverter.ToInt32(il[offset..(offset + 4)]),
            _ => 0,
        };

        return (opCode, operand, offset + size);
    }

    /// <summary>
    /// A short branch offset is signed; a short variable index is not.
    /// </summary>
    private static int ShortOperand(ILOpCode opCode, byte value) =>
        IsBranch(opCode) ? (sbyte)value : value;

    private static int OperandSize(ILOpCode opCode, ReadOnlySpan<byte> il, int offset)
    {
        if (opCode == ILOpCode.Switch)
        {
            if (offset + sizeof(int) > il.Length)
            {
                return -1;
            }

            var count = BitConverter.ToUInt32(il[offset..(offset + sizeof(int))]);

            return count > int.MaxValue / sizeof(int) ? -1 : sizeof(int) + ((int)count * sizeof(int));
        }

        return Sizes.TryGetValue((ushort)opCode, out var size) ? size : -1;
    }

    private static readonly Dictionary<ushort, int> Sizes = BuildSizes();

    private static Dictionary<ushort, int> BuildSizes()
    {
        var sizes = new Dictionary<ushort, int>();

        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            sizes[(ushort)opCode.Value] = SizeOf(opCode.OperandType);
        }

        return sizes;
    }

    /// <summary>
    /// The operand's width in bytes. A switch's is variable and worked out where it is read.
    /// </summary>
    private static int SizeOf(OperandType operandType) => operandType switch
    {
        OperandType.InlineNone or OperandType.InlineSwitch => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => sizeof(byte),
        OperandType.InlineVar => sizeof(short),
        OperandType.InlineI8 or OperandType.InlineR => sizeof(long),
        _ => sizeof(int),
    };

    private static int Index(ushort value) =>
        value > byte.MaxValue ? OneByteOpCodes + (value & byte.MaxValue) : value;

    /// <summary>
    /// How many slots a behaviour pops or pushes: one per underscore-separated part of its name,
    /// so <c>Popref_popi_pop1</c> pops three. The variable behaviours count as none; the call
    /// cases work theirs out from the signature.
    /// </summary>
    private static int Count(StackBehaviour behaviour) =>
        SlotlessBehaviours.Contains(behaviour) ? 0 : behaviour.ToString().Split('_').Length;

    private static void Pop(List<string?> stack, int count)
    {
        for (var index = 0; index < count && stack.Count > 0; index++)
        {
            stack.RemoveAt(stack.Count - 1);
        }
    }

    /// <summary>
    /// The slot <paramref name="below"/> places from the top, or null when the stack is short.
    /// </summary>
    private static string? Peek(List<string?> stack, int below) =>
        stack.Count > below ? stack[^(below + 1)] : null;

    private static string? At(IReadOnlyList<string?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;

    private static IReadOnlyList<string?> ArgumentTypes(
        MetadataReader reader,
        MethodDefinition method,
        SignatureNames names)
    {
        var types = new List<string?>();

        if (!method.Attributes.HasFlag(MethodAttributes.Static))
        {
            types.Add(MetadataNames.FullNameOf(reader, method.GetDeclaringType()));
        }

        try
        {
            types.AddRange(method.DecodeSignature(names, genericContext: null).ParameterTypes.Select(Clean));
        }
        catch (BadImageFormatException)
        {
            // Nothing to add; every argument then reads as unknown, which falls to the token.
        }

        return types;
    }

    private static IReadOnlyList<string?> LocalTypes(
        MetadataReader reader,
        MethodBodyBlock body,
        SignatureNames names)
    {
        if (body.LocalSignature.IsNil)
        {
            return [];
        }

        try
        {
            return
            [
                .. reader.GetStandaloneSignature(body.LocalSignature)
                    .DecodeLocalSignature(names, genericContext: null)
                    .Select(Clean)
            ];
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    /// <summary>
    /// Drops the by-ref marker and any instantiation, leaving the name the hierarchy is keyed on.
    /// </summary>
    private static string? Clean(string type)
    {
        var name = MetadataNames.WithoutInstantiation(type.TrimEnd('&'));

        return name.Length == 0 || name.StartsWith('!') ? null : name;
    }

    private static string? TypeNameOf(MetadataReader reader, int token, SignatureNames names)
    {
        try
        {
            var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)handle),
                HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)handle),
                HandleKind.TypeSpecification => Clean(
                    reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                        .DecodeSignature(names, genericContext: null)),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string? DeclaringTypeOf(MetadataReader reader, int token, SignatureNames names)
    {
        try
        {
            var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.MethodDefinition => MetadataNames.FullNameOf(
                    reader,
                    reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType()),
                HandleKind.MemberReference => ParentTypeOf(
                    reader,
                    reader.GetMemberReference((MemberReferenceHandle)handle).Parent,
                    names),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string? ParentTypeOf(MetadataReader reader, EntityHandle parent, SignatureNames names) =>
        parent.Kind switch
        {
            HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)parent),
            HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)parent),
            HandleKind.TypeSpecification => Clean(
                reader.GetTypeSpecification((TypeSpecificationHandle)parent)
                    .DecodeSignature(names, genericContext: null)),
            _ => null,
        };

    private static string? FieldTypeOf(MetadataReader reader, int token, SignatureNames names)
    {
        try
        {
            var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.FieldDefinition => Clean(
                    reader.GetFieldDefinition((FieldDefinitionHandle)handle).DecodeSignature(names, null)),
                HandleKind.MemberReference => Clean(
                    reader.GetMemberReference((MemberReferenceHandle)handle).DecodeFieldSignature(names, null)),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static int ArgumentCount(MetadataReader reader, int token, SignatureNames names) =>
        CallShape(reader, token, names).Parameters;

    private static (int Parameters, bool IsInstance, string? Returns) CallShape(
        MetadataReader reader,
        int token,
        SignatureNames names)
    {
        try
        {
            var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.EntityHandle(token);

            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    var signature = method.DecodeSignature(names, genericContext: null);

                    return (
                        signature.ParameterTypes.Length,
                        !method.Attributes.HasFlag(MethodAttributes.Static),
                        Returns(signature));
                }

                case HandleKind.MemberReference:
                {
                    var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                    var signature = reference.DecodeMethodSignature(names, genericContext: null);

                    return (
                        signature.ParameterTypes.Length,
                        signature.Header.IsInstance,
                        Returns(signature));
                }

                case HandleKind.MethodSpecification:
                    return CallShape(
                        reader,
                        System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(
                            reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method),
                        names);

                default:
                    return (0, false, null);
            }
        }
        catch (BadImageFormatException)
        {
            return (0, false, null);
        }
    }

    private static string? Returns(MethodSignature<string> signature) =>
        signature.ReturnType == "System.Void" ? null : Clean(signature.ReturnType) ?? "?";
}
