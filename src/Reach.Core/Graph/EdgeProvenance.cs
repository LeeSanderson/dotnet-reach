namespace Reach.Graph;

/// <summary>
/// Why an edge exists. Every edge carries it, so any selection can be explained, the cost of
/// widening can be measured, and narrowing has one class it may safely touch.
/// </summary>
/// <remarks>
/// <strong>Three classes, not two.</strong> Containment and type initialization are invented by
/// Reach, but the control flow they describe is not in doubt, so they are as non-removable as
/// compiled edges. <see cref="Widened"/> is the only speculative class, and therefore the only
/// class any future narrowing may touch.
/// </remarks>
internal enum EdgeProvenance : byte
{
    /// <summary><c>call</c> or <c>callvirt</c> to an exactly-resolved target. Read from an instruction.</summary>
    CompiledCall = 0,

    /// <summary><c>ldftn</c> or <c>ldvirtftn</c>, from the <em>capturing</em> method to the target.</summary>
    CompiledCapture = 1,

    /// <summary>Kernel method to the compiler-generated members that carry its body.</summary>
    Containment = 2,

    /// <summary>An initialization trigger to the type's <c>.cctor</c>.</summary>
    TypeInitialization = 3,

    /// <summary>A dispatch declaration to an implementation that could run.</summary>
    Widened = 4,
}

internal static class EdgeProvenances
{
    /// <summary>
    /// Read directly from an instruction. Never removable, and never the reason a selection
    /// could be narrowed.
    /// </summary>
    internal static bool IsCompiled(this EdgeProvenance provenance) =>
        provenance is EdgeProvenance.CompiledCall or EdgeProvenance.CompiledCapture;

    /// <summary>
    /// Invented by Reach where the control flow is certain but no instruction expresses it.
    /// </summary>
    internal static bool IsSynthesised(this EdgeProvenance provenance) =>
        provenance is EdgeProvenance.Containment or EdgeProvenance.TypeInitialization;

    /// <summary>The implementation that runs is not knowable, so every candidate gets an edge.</summary>
    internal static bool IsWidened(this EdgeProvenance provenance) =>
        provenance == EdgeProvenance.Widened;
}
