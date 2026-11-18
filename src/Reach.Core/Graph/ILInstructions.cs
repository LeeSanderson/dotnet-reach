using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace Reach.Graph;

/// <summary>
/// One instruction that names something in metadata.
/// </summary>
/// <param name="Offset">Where it starts, so a prefix can be told from the instruction it modifies.</param>
internal readonly record struct TokenInstruction(int Offset, ILOpCode OpCode, int Token);

/// <summary>
/// Walks a method body's IL and picks out every instruction carrying a metadata token.
/// </summary>
/// <remarks>
/// <para>
/// Instructions are decoded rather than scanned for, because an operand byte can look exactly
/// like an opcode and a false <c>call</c> would invent an edge while a missed one would lose a
/// test.
/// </para>
/// <para>
/// The operand-size table is built by reflecting over <see cref="OpCodes"/> rather than typed
/// out. The BCL owns that table; a hand-written copy is a hundred and eighty chances to be
/// wrong in a way no test would obviously catch.
/// </para>
/// </remarks>
internal static class ILInstructions
{
    private const byte TwoBytePrefix = 0xFE;
    private const ushort TwoByteBase = TwoBytePrefix << 8;

    private static readonly Dictionary<OperandType, OperandKind> Kinds = new()
    {
        [OperandType.InlineNone] = OperandKind.None,

        [OperandType.ShortInlineBrTarget] = OperandKind.One,
        [OperandType.ShortInlineI] = OperandKind.One,
        [OperandType.ShortInlineVar] = OperandKind.One,

        [OperandType.InlineVar] = OperandKind.Two,

        [OperandType.InlineBrTarget] = OperandKind.Four,
        [OperandType.InlineI] = OperandKind.Four,
        [OperandType.ShortInlineR] = OperandKind.Four,

        // Everything that names something in metadata. Reach records all of them and lets each
        // phase filter: type initialization reads the field and type tokens, widening reads
        // the constrained. prefix, and calls read the method ones.
        [OperandType.InlineField] = OperandKind.Token,
        [OperandType.InlineMethod] = OperandKind.Token,
        [OperandType.InlineSig] = OperandKind.Token,
        [OperandType.InlineString] = OperandKind.Token,
        [OperandType.InlineTok] = OperandKind.Token,
        [OperandType.InlineType] = OperandKind.Token,

        [OperandType.InlineI8] = OperandKind.Eight,
        [OperandType.InlineR] = OperandKind.Eight,

        [OperandType.InlineSwitch] = OperandKind.Switch,
    };

    private static readonly OperandKind[] SingleByte = new OperandKind[256];
    private static readonly OperandKind[] TwoByte = new OperandKind[256];

    static ILInstructions()
    {
        Array.Fill(SingleByte, OperandKind.Unknown);
        Array.Fill(TwoByte, OperandKind.Unknown);

        foreach (var field in typeof(OpCodes).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode opCode)
            {
                continue;
            }

            var value = (ushort)opCode.Value;
            var kind = KindOf(opCode.OperandType);

            if (value > byte.MaxValue)
            {
                TwoByte[value & byte.MaxValue] = kind;
            }
            else
            {
                SingleByte[value] = kind;
            }
        }
    }

    /// <summary>
    /// Every token-carrying instruction in <paramref name="il"/>, in order. An unrecognised
    /// opcode stops the walk rather than guessing at a length and reading garbage.
    /// </summary>
    internal static IReadOnlyList<TokenInstruction> Tokens(ReadOnlySpan<byte> il)
    {
        var found = new List<TokenInstruction>();
        var offset = 0;

        while (TryRead(il, ref offset, out var token))
        {
            if (token is { } instruction)
            {
                found.Add(instruction);
            }
        }

        return found;
    }

    /// <summary>
    /// Decodes the instruction at <paramref name="offset"/> and moves past it. False at the end
    /// of the body, and at anything that cannot be decoded, which ends the walk.
    /// </summary>
    private static bool TryRead(ReadOnlySpan<byte> il, ref int offset, out TokenInstruction? token)
    {
        token = null;

        if (offset >= il.Length)
        {
            return false;
        }

        var (opCode, kind, operand) = OpCodeAt(il, offset);

        var size = kind switch
        {
            OperandKind.Unknown => -1,
            OperandKind.Switch => SwitchSize(il, operand),
            OperandKind.Token => sizeof(int),
            _ => (int)kind,
        };

        if (size < 0 || operand + size > il.Length)
        {
            return false;
        }

        if (kind == OperandKind.Token)
        {
            token = new TokenInstruction(offset, opCode, BitConverter.ToInt32(il[operand..(operand + sizeof(int))]));
        }

        offset = operand + size;
        return true;
    }

    /// <summary>
    /// The opcode at <paramref name="offset"/>, its operand kind, and where its operand starts.
    /// A two-byte prefix with nothing after it reads as unknown.
    /// </summary>
    private static (ILOpCode OpCode, OperandKind Kind, int Operand) OpCodeAt(ReadOnlySpan<byte> il, int offset)
    {
        var first = il[offset];

        if (first != TwoBytePrefix)
        {
            return ((ILOpCode)first, SingleByte[first], offset + 1);
        }

        if (offset + 1 >= il.Length)
        {
            return (default, OperandKind.Unknown, offset + 1);
        }

        var second = il[offset + 1];

        return ((ILOpCode)(TwoByteBase | second), TwoByte[second], offset + sizeof(ushort));
    }

    private static int SwitchSize(ReadOnlySpan<byte> il, int offset)
    {
        if (offset + sizeof(int) > il.Length)
        {
            return -1;
        }

        var count = BitConverter.ToUInt32(il[offset..(offset + sizeof(int))]);

        return count > int.MaxValue / sizeof(int) ? -1 : sizeof(int) + ((int)count * sizeof(int));
    }

    private static OperandKind KindOf(OperandType operandType) =>
        Kinds.GetValueOrDefault(operandType, OperandKind.Unknown);

    private enum OperandKind
    {
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        Eight = 8,

        /// <summary>
        /// Four bytes, and the value is a metadata token worth recording.
        /// </summary>
        Token = -1000,

        /// <summary>
        /// A count followed by that many four-byte targets.
        /// </summary>
        Switch = -2000,

        Unknown = -3000,
    }
}
