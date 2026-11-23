using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Reach.Projects;
using Reach.Reporting;

namespace Reach.Changes;

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
        if (!decimal.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        return !LanguageVersionFacts.TryParse(value, out var parsed)
            || parsed > LanguageVersionFacts.MapSpecifiedToEffectiveVersion(ParserCeiling);
    }

    /// <summary>
    /// The <c>langversion-above-ceiling</c> notice for every project whose declared
    /// <c>LangVersion</c> exceeds the ceiling, or <c>null</c> when none does.
    /// </summary>
    /// <remarks>
    /// Reported, <strong>not</strong> widened — see <see cref="ExceedsCeiling"/>. Nothing about
    /// selection reads this.
    /// </remarks>
    internal static Notice? AboveCeilingNotice(IEnumerable<ProjectFile> projects)
    {
        var above = projects
            .Where(project => ExceedsCeiling(project.LangVersion))
            .OrderBy(project => project.Path, StringComparer.Ordinal)
            .ToArray();

        if (above.Length == 0)
        {
            return null;
        }

        var named = above.Select(project => $"{project.Name} (LangVersion {project.LangVersion})");

        return Notice.Environment(
            NoticeCodes.LangVersionAboveCeiling,
            $"{above.Length} project(s) declare a language version above what Reach's parser "
            + $"understands: {string.Join(", ", named)}. Newer syntax can misparse without any "
            + "diagnostic, so a change inside it may be missed. Reported only; nothing was widened.",
            ("projects", above.Select(project => project.Path).ToArray()));
    }
}
