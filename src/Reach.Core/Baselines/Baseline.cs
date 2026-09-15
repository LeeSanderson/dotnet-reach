namespace Reach.Baselines;

/// <summary>
/// The commit a change is measured against, and how Reach arrived at it. Always
/// <c>merge-base(HEAD, reference)</c> — never the reference's own tip.
/// </summary>
/// <param name="Sha">The resolved commit.</param>
/// <param name="Reference">The reference the merge-base was taken against.</param>
/// <param name="Origin">Which rung of the detection ladder produced <paramref name="Reference"/>.</param>
/// <param name="IsHead">
/// The baseline is <c>HEAD</c> itself, so only the working tree can differ from it. Disclosed
/// whether or not the change set turns out empty.
/// </param>
internal sealed record Baseline(string Sha, string Reference, BaselineOrigin Origin, bool IsHead);
