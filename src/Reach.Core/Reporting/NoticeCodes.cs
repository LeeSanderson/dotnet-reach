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
    internal const string UntrackedSourceFiles = "untracked-source-files";

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

    /// <summary>
    /// The rendered filter matches more tests than were selected, because the dialect matches
    /// by containment rather than equality.
    /// </summary>
    internal const string DialectOverSelects = "dialect-over-selects";

    /// <summary>
    /// A selection exceeded the command-line ceiling and went through a response file or across
    /// several invocations.
    /// </summary>
    internal const string SelectionDeliveredOutOfBand = "selection-delivered-out-of-band";

    /// <summary>
    /// The caller's runsettings file already carries a <c>TestCaseFilter</c>, which would be
    /// AND-ed with Reach's, so the project runs in full instead.
    /// </summary>
    internal const string RunSettingsFilterConflict = "runsettings-filter-conflict";

    /// <summary>
    /// A multi-targeted project carries a platform suffix, whose declared moniker cannot be
    /// recovered from metadata, so no <c>-f</c> selector can be rendered.
    /// </summary>
    internal const string FrameworkSelectorUnderivable = "framework-selector-underivable";

    /// <summary>
    /// A changed file matched no rule in the tier ladder's table and sits inside no project, so
    /// nothing was selected for it. The one rule in Reach that errs toward selecting nothing.
    /// </summary>
    internal const string UnmappedFileNoProject = "unmapped-file-no-project";

    /// <summary>
    /// A changed file produced an error diagnostic or skipped tokens, so its project widened.
    /// Reach's parser did not understand the source it was given.
    /// </summary>
    internal const string ParseFailed = "parse-failed";

    /// <summary>
    /// A project declares a language version above what Reach's parser understands. Reported
    /// rather than widened — the window is narrow and widening would fire across whole modern
    /// codebases for a hazard that usually is not present.
    /// </summary>
    internal const string LangVersionAboveCeiling = "langversion-above-ceiling";

    /// <summary>
    /// A removal or a changed compile-time constant widened every assembly that transitively
    /// references the declaring one.
    /// </summary>
    internal const string RecompilationWidening = "recompilation-widening";

    /// <summary>
    /// NUnit's filter matches by containment rather than equality, so the rendered filter can
    /// run tests the selection did not name. A standing disclosure, emitted whenever a project
    /// renders into that dialect at all.
    /// </summary>
    internal const string NUnitFilterOvermatch = "nunit-filter-overmatch";

    /// <summary>
    /// A function pointer was invoked through <c>calli</c>. Where the pointer came from is not
    /// in the analysed instructions, so no edge could be made.
    /// </summary>
    internal const string CalliUnresolved = "calli-unresolved";

    /// <summary>
    /// An <c>xunit.v3</c> 4.0.0 project in a repository with no <c>global.json</c> runner
    /// setting, where <c>dotnet test</c> is a hard build error — so every invocation Reach
    /// emits is unrunnable and the error names nothing Reach-shaped.
    /// </summary>
    internal const string TestRunnerNotConfigured = "test-runner-not-configured";
}
