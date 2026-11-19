using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace Reach.Graph;

/// <summary>
/// Decodes a method body's IL the way an abstract interpretation of the evaluation stack needs
/// it: every instruction with its operand read as an integer, where branches and handlers can
/// arrive, and how many slots an instruction pops and pushes.
/// </summary>
/// <remarks>
/// Every table here is read from <see cref="OpCodes"/> rather than typed out: the BCL owns them,
/// and a hand-written copy is hundreds of chances to be wrong in a way no test would catch.
/// </remarks>
internal static class ILDecoder
{
    private const byte TwoBytePrefix = 0xFE;

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
    /// branches and <c>leave</c>, long and short.
    /// </summary>
    private static readonly HashSet<ILOpCode> Branches =
    [
        .. typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => field.GetValue(null))
            .OfType<OpCode>()
            .Where(opCode => opCode.OperandType is OperandType.InlineBrTarget or OperandType.ShortInlineBrTarget)
            .Select(opCode => OpCodeValues.ToOpCode(OpCodeValues.ValueOf(opCode))),
    ];

    private static readonly Dictionary<ushort, int> Sizes = BuildSizes();

    static ILDecoder()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var index = Index(OpCodeValues.ValueOf(opCode));

            Pops[index] = Count(opCode.StackBehaviourPop);
            Pushes[index] = Count(opCode.StackBehaviourPush);
        }
    }

    /// <summary>
    /// Decodes one instruction: its opcode, its operand as an integer, and where the next one starts.
    /// One it cannot decode reads as a <c>nop</c> with a negative next offset.
    /// </summary>
    internal static (ILOpCode OpCode, int Operand, int Next) Read(ReadOnlySpan<byte> il, int offset)
    {
        var first = il[offset++];
        ILOpCode opCode;

        if (first == TwoBytePrefix)
        {
            if (offset >= il.Length)
            {
                return (ILOpCode.Nop, 0, -1);
            }

            opCode = OpCodeValues.ToTwoByteOpCode(il[offset++]);
        }
        else
        {
            opCode = OpCodeValues.ToOpCode(first);
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
    /// Offsets a branch or a handler can arrive at, where the stack stops being knowable.
    /// </summary>
    internal static HashSet<int> Boundaries(ReadOnlySpan<byte> il, MethodBodyBlock body)
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

    /// <summary>
    /// How many slots <paramref name="opCode"/> pops and pushes, by its fixed stack behaviour.
    /// The variable behaviours count as none; the call instructions work theirs out from the
    /// signature.
    /// </summary>
    internal static (int Pops, int Pushes) StackEffectOf(ILOpCode opCode)
    {
        var index = Index(OpCodeValues.ValueOf(opCode));

        return (index < Pops.Length ? Pops[index] : 0, index < Pushes.Length ? Pushes[index] : 0);
    }

    private static bool IsBranch(ILOpCode opCode) => Branches.Contains(opCode);

    /// <summary>
    /// A short branch offset is signed; a short variable index is not.
    /// </summary>
    private static int ShortOperand(ILOpCode opCode, byte value) =>
        IsBranch(opCode) ? sbyte.CreateTruncating(value) : value;

    private static int OperandSize(ILOpCode opCode, ReadOnlySpan<byte> il, int offset)
    {
        if (opCode == ILOpCode.Switch)
        {
            if (offset + sizeof(int) > il.Length)
            {
                return -1;
            }

            var count = BitConverter.ToUInt32(il[offset..(offset + sizeof(int))]);

            return count > int.MaxValue / sizeof(int) ? -1 : sizeof(int) + (int.CreateTruncating(count) * sizeof(int));
        }

        return Sizes.TryGetValue(OpCodeValues.ValueOf(opCode), out var size) ? size : -1;
    }

    private static Dictionary<ushort, int> BuildSizes()
    {
        var sizes = new Dictionary<ushort, int>();

        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            sizes[OpCodeValues.ValueOf(opCode)] = SizeOf(opCode.OperandType);
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
    /// so <c>Popref_popi_pop1</c> pops three. The variable behaviours count as none.
    /// </summary>
    private static int Count(StackBehaviour behaviour) =>
        SlotlessBehaviours.Contains(behaviour) ? 0 : behaviour.ToString().Split('_').Length;
}
