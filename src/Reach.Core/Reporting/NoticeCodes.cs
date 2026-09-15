namespace Reach.Reporting;

/// <summary>
/// Every notice code Reach emits. Kebab-case and never renamed: a consumer gating on one is
/// depending on the string, and the limitations register anchors on it.
/// </summary>
/// <remarks>
/// The catalogue that binds each code to its kind, and the parity test that fails the build
/// when a <see cref="NoticeKind.BlindSpot"/> code has no register entry, arrive with the
/// notice catalogue. Codes are added here as the phase that emits them lands.
/// </remarks>
internal static class NoticeCodes
{
    /// <summary>How the baseline was arrived at, and what SHA it resolved to.</summary>
    internal const string BaselineResolved = "baseline-resolved";

    /// <summary>The baseline resolved to <c>HEAD</c>, so the change set can only be the working tree.</summary>
    internal const string BaselineIsHead = "baseline-is-head";

    /// <summary>No baseline could be resolved. The run failed; the message says how to fix it.</summary>
    internal const string BaselineUnresolvable = "baseline-unresolvable";

    /// <summary>
    /// Reach was pointed at a project that no solution above it lists, so there is no expected
    /// project list to check the build output against.
    /// </summary>
    internal const string ProjectOutsideEverySolution = "project-outside-every-solution";

    /// <summary>
    /// A C# file that git has never seen entered the changed set — intentional code
    /// generation, or a forgotten <c>git add</c>.
    /// </summary>
    internal const string UntrackedSourceInChangeSet = "untracked-source-in-change-set";

    /// <summary>
    /// An assembly was compiled from source that is untracked <em>and</em> git-ignored, so a
    /// change to it is invisible to Reach. A blind spot, with an entry in the register.
    /// </summary>
    internal const string IgnoredUntrackedAssembly = "ignored-untracked-assembly";

    /// <summary>
    /// A call site names a member of a first-party assembly that is not in it. A build
    /// problem rather than an analysis one, and the run continues.
    /// </summary>
    internal const string UnresolvedFirstPartyMember = "unresolved-first-party-member";

    /// <summary>
    /// A signature matched more than one method, so every candidate got an edge. Widening is
    /// the safe direction.
    /// </summary>
    internal const string SignatureAmbiguous = "signature-ambiguous";

    /// <summary>
    /// A test project uses a framework Reach does not recognise, so every test in it runs and
    /// its total is unknown rather than zero.
    /// </summary>
    internal const string WholeProjectFallback = "whole-project-fallback";

    // The emptiness taxonomy. `reasons` is a list of these, never prose, and required non-empty
    // when the outcome is nothing-selected — so every cause of emptiness is documented and
    // register-linked, and nobody adds one as an ad-hoc string.

    /// <summary>There were changes, they joined, and no test could reach any of them.</summary>
    internal const string NoChangedMemberReachedATest = "no-changed-member-reached-a-test";

    /// <summary>Every change was outside the union of the test projects' closures.</summary>
    internal const string AllChangesOutsideAnalysisScope = "all-changes-outside-analysis-scope";

    /// <summary>
    /// Paths changed, but nothing inside them did: comments, whitespace, or a declaration whose
    /// canonical form is unchanged.
    /// </summary>
    internal const string ChangesWereFormattingOnly = "changes-were-formatting-only";
}
