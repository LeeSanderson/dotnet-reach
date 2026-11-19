namespace Reach.Graph;

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
        provenance is EdgeProvenance.Containment
            or EdgeProvenance.TypeInitialization
            or EdgeProvenance.TestLifecycle;

    /// <summary>
    /// The implementation that runs is not knowable, so every candidate gets an edge.
    /// </summary>
    internal static bool IsWidened(this EdgeProvenance provenance) =>
        provenance == EdgeProvenance.Widened;
}
