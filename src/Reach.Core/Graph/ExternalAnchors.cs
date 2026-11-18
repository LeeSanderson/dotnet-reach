namespace Reach.Graph;

/// <summary>
/// Members of assemblies Reach never reads, interned so widening has something to hang off.
/// </summary>
/// <remarks>
/// Roslyn emits <c>callvirt</c> against the slot-defining declaration, which is routinely
/// <c>object::ToString</c> or <c>System.IDisposable::Dispose</c>, and those assemblies are not
/// in the analysis scope — so there is no <c>MethodDefinition</c> token to name them by. An
/// anchor is interned only where a first-party type occupies the slot, which bounds the set by
/// the solution's own shape rather than by the BCL's size.
/// </remarks>
internal sealed class ExternalAnchors
{
    private readonly Dictionary<string, int> _ordinalOf = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int Ordinal, string Key), int> _idOf = [];
    private readonly Dictionary<MethodId, string> _nameOf = [];

    internal int AssemblyCount => _ordinalOf.Count;

    internal int Count => _idOf.Count;

    /// <param name="nextOrdinal">
    /// Where to number a newly-seen assembly. External assemblies sit above the first-party
    /// ones, in first-encounter order during a pass that itself runs in sorted order.
    /// </param>
    internal MethodId Intern(string assemblyName, string key, int nextOrdinal)
    {
        if (!_ordinalOf.TryGetValue(assemblyName, out var ordinal))
        {
            _ordinalOf[assemblyName] = ordinal = nextOrdinal;
        }

        if (!_idOf.TryGetValue((ordinal, key), out var id))
        {
            _idOf[(ordinal, key)] = id = _idOf.Count + 1;
        }

        var methodId = MethodId.External(ordinal, id);
        _nameOf[methodId] = assemblyName + "!" + key;

        return methodId;
    }

    /// <summary>
    /// For messages and the report only. Never part of identity.
    /// </summary>
    internal string? NameOf(MethodId id) => _nameOf.GetValueOrDefault(id);
}
