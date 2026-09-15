using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Reach.Changes;

/// <summary>What reading one revision of one file told Reach about its own parser.</summary>
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

/// <summary>
/// Detecting the C# Reach cannot read.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The naive rule — any parse error widens — is not a safety net.</strong> Roslyn checks
/// language versions at <em>binding</em>, not at parsing, and Reach only ever calls
/// <c>ParseText</c>, so an older parser meeting newer C# never throws. It fails three ways: an
/// error diagnostic with local structural damage, an error diagnostic with the structure intact,
/// and — the one nothing here can see — a <strong>clean parse that is structurally wrong with
/// zero diagnostics</strong>, as when <c>record Person(string First)</c> reads as a method named
/// <c>Person</c> returning a type called <c>record</c>.
/// </para>
/// <para>
/// <strong>A clean parse never proves a correct parse.</strong> The mitigation for the third row
/// is not detection — it is parsing with <see cref="LanguageVersion.Preview"/> and shipping
/// current Roslyn. The hole is also narrower than it looks: the <em>same</em> parser reads both
/// revisions, so a deterministic misparse still diffs stably, and a phantom member simply fails
/// the join and falls through to whole-assembly widening.
/// </para>
/// </remarks>
internal static class ParseHealthCheck
{
    /// <summary>
    /// The one argument that matters, asserted rather than assumed. <c>Latest</c> would pin the
    /// parser to the shipped language version and misparse anything newer, silently.
    /// </summary>
    internal static LanguageVersion ParserCeiling => LanguageVersion.Preview;

    internal static ParseHealth Of(string text)
    {
        if (text.Length == 0)
        {
            return new ParseHealth(false, false);
        }

        var tree = SourceRevision.Parse(text);
        var root = tree.GetRoot();

        return new ParseHealth(
            tree.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            root.DescendantTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SkippedTokensTrivia)));
    }

    /// <summary>
    /// Whether a project's declared <c>LangVersion</c> is above what Reach's parser understands.
    /// </summary>
    /// <remarks>
    /// This produces a notice and <strong>not</strong> widening. With current Roslyn shipped the
    /// window is narrow, and widening on it would fire across whole modern codebases for a
    /// hazard that usually is not present.
    /// </remarks>
    internal static bool ExceedsCeiling(string? declared)
    {
        if (string.IsNullOrWhiteSpace(declared))
        {
            return false;
        }

        var value = declared.Trim();

        // `preview`, `latest` and `default` are all at or below the ceiling by construction.
        if (!decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var version))
        {
            return false;
        }

        return !LanguageVersionFacts.TryParse(value, out var parsed)
            || parsed > LanguageVersionFacts.MapSpecifiedToEffectiveVersion(ParserCeiling);
    }
}
