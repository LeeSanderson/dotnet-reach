namespace Reach.Reporting;

/// <summary>
/// Every notice code Reach can emit, with its kind.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue is what the limitations register is checked against: every
/// <see cref="NoticeKind.BlindSpot"/> code must have an entry there, and Reach's own test suite
/// asserts the two sets match exactly. That single test is what converts "named and surfaced"
/// from a promise into a build failure, and it is why the register can be a hand-written
/// document without drifting.
/// </para>
/// <para>
/// It also answers detectability from the clean side: a gap Reach can detect <em>has</em> a code
/// and is mechanically tied to an entry; a gap it cannot detect has no code, and the document is
/// the only place it can live.
/// </para>
/// </remarks>
internal static class NoticeCatalogue
{
    internal static IReadOnlyDictionary<string, NoticeKind> All { get; } =
        new Dictionary<string, NoticeKind>(StringComparer.Ordinal)
        {
            // Environment — facts about the run, not about the analysis.
            [NoticeCodes.BaselineResolved] = NoticeKind.Environment,
            [NoticeCodes.BaselineIsHead] = NoticeKind.Environment,
            [NoticeCodes.BaselineUnresolvable] = NoticeKind.Environment,
            [NoticeCodes.LangVersionAboveCeiling] = NoticeKind.Environment,
            [NoticeCodes.SelectionDeliveredOutOfBand] = NoticeKind.Environment,
            [NoticeCodes.TestRunnerNotConfigured] = NoticeKind.Environment,

            // Scope — something fell outside what Reach analyses, or into it unexpectedly.
            [NoticeCodes.ProjectOutsideEverySolution] = NoticeKind.Scope,
            [NoticeCodes.UntrackedSourceFiles] = NoticeKind.Scope,
            [NoticeCodes.NoChangedMemberReachedATest] = NoticeKind.Scope,
            [NoticeCodes.AllChangesOutsideAnalysisScope] = NoticeKind.Scope,
            [NoticeCodes.ChangesWereFormattingOnly] = NoticeKind.Scope,

            // Widening — may over-select, which is wasteful and safe.
            [NoticeCodes.SignatureAmbiguous] = NoticeKind.Widening,
            [NoticeCodes.WholeProjectFallback] = NoticeKind.Widening,
            [NoticeCodes.DialectOverSelects] = NoticeKind.Widening,
            [NoticeCodes.NUnitFilterOvermatch] = NoticeKind.Widening,
            [NoticeCodes.RunSettingsFilterConflict] = NoticeKind.Widening,
            [NoticeCodes.FrameworkSelectorUnderivable] = NoticeKind.Widening,
            [NoticeCodes.RecompilationWidening] = NoticeKind.Widening,

            // Blind spots — may under-select. Every one of these needs a register entry, and
            // the parity test is what makes that true rather than aspirational.
            [NoticeCodes.IgnoredUntrackedAssembly] = NoticeKind.BlindSpot,
            [NoticeCodes.UnmappedFileNoProject] = NoticeKind.BlindSpot,
            [NoticeCodes.ParseFailed] = NoticeKind.BlindSpot,
            [NoticeCodes.CalliUnresolved] = NoticeKind.BlindSpot,
            [NoticeCodes.UnresolvedFirstPartyMember] = NoticeKind.BlindSpot,
        };

    internal static IReadOnlyList<string> Of(NoticeKind kind) =>
    [
        .. All.Where(entry => entry.Value == kind)
            .Select(entry => entry.Key)
            .Order(StringComparer.Ordinal)
    ];

    /// <summary>Whether a code is one Reach knows about. Nothing outside the catalogue may be emitted.</summary>
    internal static bool Knows(string code) => All.ContainsKey(code);
}
