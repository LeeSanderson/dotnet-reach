using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;

namespace Reach.Graph;

/// <summary>
/// Moves between an opcode and its encoded value: the one byte, or the <c>0xFE</c> prefix and
/// the byte after it, read as a big-endian <see cref="ushort"/>.
/// </summary>
/// <remarks>
/// The IL walkers decode every instruction of every method body, so these reinterpret the bits
/// rather than box through <see cref="Convert"/>.
/// </remarks>
internal static class OpCodeValues
{
    private const int TwoBytePrefix = 0xFE00;

    /// <summary>
    /// The opcode an encoded value names. A value no opcode has passes through unchanged.
    /// </summary>
    internal static ILOpCode ToOpCode(ushort value) => Unsafe.BitCast<ushort, ILOpCode>(value);

    /// <summary>
    /// The opcode a two-byte encoding names, given the byte after the <c>0xFE</c> prefix.
    /// </summary>
    internal static ILOpCode ToTwoByteOpCode(byte second) => ToOpCode(ushort.CreateTruncating(TwoBytePrefix | second));

    /// <summary>
    /// The encoded value of <paramref name="opCode"/>.
    /// </summary>
    internal static ushort ValueOf(ILOpCode opCode) => Unsafe.BitCast<ILOpCode, ushort>(opCode);

    /// <summary>
    /// The encoded value of a reflection <see cref="OpCode"/>, whose <see cref="OpCode.Value"/>
    /// is signed, so every two-byte opcode reads as negative.
    /// </summary>
    internal static ushort ValueOf(OpCode opCode) => ushort.CreateTruncating(opCode.Value);
}
