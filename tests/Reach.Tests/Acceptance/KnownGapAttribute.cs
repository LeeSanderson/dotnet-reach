using Xunit.v3;

namespace Reach.Tests.Acceptance;

/// <summary>
/// Marks a known-gap test (spec §4.5): one that pins a deliberate over-selection or a documented
/// under-selection with an exact expected result, and turns red when the gap closes.
/// </summary>
/// <remarks>
/// <para>
/// It is the trait <c>Batch=known-gap</c>, which is what the release gate excludes:
/// <c>--filter-not-trait "Batch=known-gap"</c>. Known gaps are never in the gate.
/// </para>
/// <para>
/// The method's name starts <c>Known_gap_</c> as well, for the human reader. The guard in
/// <c>Harness/KnownGapGuardTests.cs</c> fails where the two disagree.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class KnownGapAttribute : Attribute, ITraitAttribute
{
    /// <summary>
    /// The trait's name.
    /// </summary>
    internal const string Name = "Batch";

    /// <summary>
    /// The trait's value.
    /// </summary>
    internal const string Value = "known-gap";

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() => [new(Name, Value)];
}
