namespace Reach.Graph;

/// <summary>
/// A method's node in the call graph, derived from metadata rather than from its name.
/// </summary>
/// <remarks>
/// <code>
/// bit 63      : discriminator
/// bits 62..32 : assembly-instance ordinal (31 bits)
/// bits 31..0  : metadata token, or interned external-reference id (32 bits)
/// </code>
/// <para>
/// No strings, ever. A metadata token is one byte of table id plus three of row index, so 32
/// bits is exact; 31 bits of ordinal is four orders of magnitude more assemblies than any
/// solution has.
/// </para>
/// <para>
/// <strong>The target framework needs no field.</strong> An assembly instance <em>is</em> one
/// (project, target framework) pair and each gets its own ordinal, so multi-targeting falls
/// out of the ordinal and the hottest struct in the tool stays eight bytes. This is the single
/// simplification that makes the representation fit; do not add a field.
/// </para>
/// </remarks>
internal readonly record struct MethodId(ulong Value)
{
    private const int OrdinalShift = 32;
    private const ulong Discriminator = 1UL << 63;
    private const ulong LowMask = 0xFFFF_FFFFUL;
    private const ulong OrdinalMask = 0x7FFF_FFFFUL;

    internal const int MaxOrdinal = (int)OrdinalMask;

    /// <summary>A method Reach has the metadata for.</summary>
    internal static MethodId Definition(int ordinal, int token) =>
        new(((ulong)(uint)ordinal << OrdinalShift) | ((uint)token & LowMask));

    /// <summary>
    /// A widening anchor in an assembly Reach never reads. Roslyn emits <c>callvirt</c> against
    /// the slot-defining declaration, which is routinely <c>object::ToString</c> or
    /// <c>System.IDisposable::Dispose</c>, and there is no <c>MethodDefinition</c> token to
    /// name those by.
    /// </summary>
    /// <remarks>
    /// External nodes are widening anchors only: never roots, never walked into, no body ever
    /// read. The interned set is bounded by the slots first-party types actually implement or
    /// override, not by the BCL's size.
    /// </remarks>
    internal static MethodId External(int ordinal, int interned) =>
        new(Discriminator | ((ulong)(uint)ordinal << OrdinalShift) | ((uint)interned & LowMask));

    internal bool IsExternal => (Value & Discriminator) != 0;

    internal int Ordinal => (int)((Value >> OrdinalShift) & OrdinalMask);

    /// <summary>The metadata token, or the interned reference id when <see cref="IsExternal"/>.</summary>
    internal int Token => (int)(Value & LowMask);

    public override string ToString() =>
        IsExternal ? $"external:{Ordinal}#{Token}" : $"{Ordinal}:0x{Token:x8}";
}
