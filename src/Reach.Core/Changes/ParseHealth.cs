namespace Reach.Changes;

/// <summary>
/// What reading one revision of one file told Reach about its own parser.
/// </summary>
/// <param name="HasErrorDiagnostic">Roslyn produced an error. Visible, and widens.</param>
/// <param name="HasSkippedTokens">
/// Roslyn dropped tokens from the tree. A <em>structural</em> signal rather than a diagnostic
/// one, which is why it earns its own place: a construct whose members are swallowed is the
/// genuine hole, because a changed method inside one never enters the changed set at all.
/// </param>
internal readonly record struct ParseHealth(bool HasErrorDiagnostic, bool HasSkippedTokens)
{
    internal bool IsSound => !HasErrorDiagnostic && !HasSkippedTokens;
}
