using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace Reach.Graph;

/// <summary>One instruction that names something in metadata.</summary>
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

            if (value > 0xFF)
            {
                TwoByte[value & 0xFF] = kind;
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

        while (offset < il.Length)
        {
            var start = offset;
            var first = il[offset++];
            ILOpCode opCode;
            OperandKind kind;

            if (first == TwoBytePrefix)
            {
                if (offset >= il.Length)
                {
                    break;
                }

                var second = il[offset++];
                opCode = (ILOpCode)((TwoBytePrefix << 8) | second);
                kind = TwoByte[second];
            }
            else
            {
                opCode = (ILOpCode)first;
                kind = SingleByte[first];
            }

            if (kind == OperandKind.Unknown)
            {
                break;
            }

            var size = kind switch
            {
                OperandKind.Switch => SwitchSize(il, offset),
                OperandKind.Token => 4,
                _ => (int)kind,
            };

            if (size < 0 || offset + size > il.Length)
            {
                break;
            }

            if (kind == OperandKind.Token)
            {
                found.Add(new TokenInstruction(start, opCode, BitConverter.ToInt32(il[offset..(offset + 4)])));
            }

            offset += size;
        }

        return found;
    }

    private static int SwitchSize(ReadOnlySpan<byte> il, int offset)
    {
        if (offset + 4 > il.Length)
        {
            return -1;
        }

        var count = BitConverter.ToUInt32(il[offset..(offset + 4)]);

        return count > int.MaxValue / 4 ? -1 : 4 + ((int)count * 4);
    }

    private static OperandKind KindOf(OperandType operandType) => operandType switch
    {
        OperandType.InlineNone => OperandKind.None,

        OperandType.ShortInlineBrTarget
            or OperandType.ShortInlineI
            or OperandType.ShortInlineVar => OperandKind.One,

        OperandType.InlineVar => OperandKind.Two,

        OperandType.InlineBrTarget
            or OperandType.InlineI
            or OperandType.ShortInlineR => OperandKind.Four,

        // Everything that names something in metadata. Reach records all of them and lets each
        // phase filter: type initialization reads the field and type tokens, widening reads
        // the constrained. prefix, and calls read the method ones.
        OperandType.InlineField
            or OperandType.InlineMethod
            or OperandType.InlineSig
            or OperandType.InlineString
            or OperandType.InlineTok
            or OperandType.InlineType => OperandKind.Token,

        OperandType.InlineI8 or OperandType.InlineR => OperandKind.Eight,

        OperandType.InlineSwitch => OperandKind.Switch,

        _ => OperandKind.Unknown,
    };

    private enum OperandKind
    {
        None = 0,
        One = 1,
        Two = 2,
        Four = 4,
        Eight = 8,

        /// <summary>Four bytes, and the value is a metadata token worth recording.</summary>
        Token = -1000,

        /// <summary>A count followed by that many four-byte targets.</summary>
        Switch = -2000,

        Unknown = -3000,
    }
}
