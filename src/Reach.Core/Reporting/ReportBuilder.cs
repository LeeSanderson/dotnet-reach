using Reach.Assemblies;
using Reach.Baselines;
using Reach.Changes;
using Reach.Rendering;
using Reach.Selection;

namespace Reach.Reporting;

/// <summary>
/// Turns what a run concluded into the report it writes.
/// </summary>
/// <remarks>
/// <strong>A report is written even when the outcome is <c>failed</c></strong>, because a tool
/// that writes nothing when it fails is one you debug by re-running it with more flags, on CI,
/// which is the worst place to need a second run. The one exception is a usage error, whose
/// arguments were never valid enough to establish where to write one.
/// </remarks>
internal static class ReportBuilder
{
    internal static Report Build(SelectRun run, SelectRequest request, PhaseTimings timings)
    {
        var outcome = OutcomeOf(run);
        var entries = EntriesOf(run);

        return new Report
        {
            Outcome = outcome.Wire(),
            Reasons = outcome == Outcome.NothingSelected ? Reasons(run) : null,
            Envelope = EnvelopeOf(run, request, timings),
            Scope = ScopeOf(run),
            Changes = ChangesOf(run),
            Entries = entries,
            Notices = NoticesOf(run),
        };
    }

    /// <summary>
    /// An empty selection and an empty change set are different news. They exit the same and
    /// mean opposite things — a docs-only pull request, against code changes that reached
    /// nothing, which is either a coverage gap or an under-selection bug.
    /// </summary>
    internal static Outcome OutcomeOf(SelectRun run)
    {
        if (run.ExitCode != ExitCode.Success)
        {
            return Outcome.Failed;
        }

        if (run.Changes is null || run.Changes.IsEmpty)
        {
            return Outcome.NoChanges;
        }

        return run.Selection?.AnythingSelected == true ? Outcome.Selected : Outcome.NothingSelected;
    }

    /// <summary>
    /// Never prose, and never empty when the outcome is <c>nothing-selected</c>. The order is
    /// most-specific first, because the reason codes lead the human summary and this is the
    /// result most likely to be disbelieved.
    /// </summary>
    private static IReadOnlyList<string> Reasons(SelectRun run)
    {
        var reasons = new List<string>();

        if (run.Changes is { } changes && changes.Members.Count == 0 && changes.Paths.Count > 0)
        {
            reasons.Add(NoticeCodes.ChangesWereFormattingOnly);
        }

        if (run.Selection is { } selection && selection.Changes.Count == 0)
        {
            reasons.Add(NoticeCodes.AllChangesOutsideAnalysisScope);
        }

        if (reasons.Count == 0)
        {
            reasons.Add(NoticeCodes.NoChangedMemberReachedATest);
        }

        return reasons;
    }

    private static ReportEnvelope EnvelopeOf(SelectRun run, SelectRequest request, PhaseTimings timings) =>
        new()
        {
            ToolVersion = Product.Version,
            Baseline = run.Baseline is { } baseline
                ? new ReportBaseline(
                    baseline.Sha,
                    baseline.Reference,
                    baseline.Origin.ToString(),
                    baseline.IsHead)
                : null,
            Head = run.Head,
            ChangeSources = ["committed", "staged", "unstaged", "untracked"],
            BuildMode = request.NoBuild ? "no-build" : "build",
            ForwardedBuildArguments = request.ForwardedBuildArguments,
            Correspondence = (run.Correspondence?.Verdict ?? CorrespondenceVerdict.Skipped)
                .ToString()
                .ToLowerInvariant(),
            Timings = timings.ToReport(),
        };

    /// <summary>
    /// Repository-relative and forward-slashed, which is the spelling git uses and the only one
    /// that means the same thing on another machine. An absolute build-agent path in a report
    /// people read and diff is noise at best.
    /// </summary>
    private static string Relative(string path, string? root) =>
        root is null or { Length: 0 }
            ? path.Replace('\\', '/')
            : Path.GetRelativePath(root, path).Replace('\\', '/');

    private static ReportScope ScopeOf(SelectRun run) =>
        new()
        {
            TestProjects =
            [
                .. (run.Scope?.TestProjects ?? [])
                    .Select(project => Relative(project.Path, run.RepositoryRoot))
                    .Order(StringComparer.Ordinal)
            ],
            Assemblies =
            [
                .. (run.Assemblies ?? [])
                    .Select(instance => new ReportAssembly(
                        instance.Assembly.SimpleName,
                        instance.Expected.TargetFramework))
                    .OrderBy(assembly => assembly.Name, StringComparer.Ordinal)
                    .ThenBy(assembly => assembly.TargetFramework, StringComparer.Ordinal)
            ],
        };

    /// <summary>Sorted by display form, which is the documented key.</summary>
    private static IReadOnlyList<ReportChange> ChangesOf(SelectRun run) =>
    [
        .. (run.Selection?.Changes ?? [])
            .Select(change => new ReportChange(
                change.Index,
                change.Display,
                Kebab(change.Tier.ToString()),
                change.Reason,
                change.TestsReached))
            .OrderBy(change => change.Display, StringComparer.Ordinal)
            .ThenBy(change => change.Index)
    ];

    /// <summary>
    /// Every test project appears even with an empty selection, so a pipeline can skip it
    /// deliberately rather than by absence — and on <c>no-changes</c> the full list is still
    /// emitted, every entry <c>skip</c> with zero invocations, so a consumer's loop behaves
    /// identically across all four outcomes.
    /// </summary>
    private static IReadOnlyList<ReportEntry> EntriesOf(SelectRun run) =>
    [
        .. (run.Rendered?.Entries ?? [])
            .Select(rendered => EntryOf(rendered, run.RepositoryRoot))
            .OrderBy(entry => entry.Project, StringComparer.Ordinal)
            .ThenBy(entry => entry.TargetFramework, StringComparer.Ordinal)
    ];

    private static ReportEntry EntryOf(RenderedEntry rendered, string? root)
    {
        var project = rendered.Selection;

        return new ReportEntry
        {
            Project = Relative(project.Project.Path, root),
            TargetFramework = project.TargetFramework,
            Framework = project.Dialect,
            Dialect = project.Dialect,
            RunnerHost = rendered.Mode == SelectionMode.Skip ? null : "dotnet-test",
            Mode = Kebab(rendered.Mode.ToString()),
            Delivery = rendered.Delivery == Delivery.None ? null : Kebab(rendered.Delivery.ToString()),
            Counts = new ReportCounts(
                project.Selected.Count,
                rendered.Mode == SelectionMode.RunAll ? (project.Total ?? 0) : rendered.WillRun,
                project.Total),
            Tests =
            [
                .. project.Selected
                    .Select(TestOf)
                    .OrderBy(test => test.Display, StringComparer.Ordinal)
            ],
            Invocations = rendered.Invocations,
        };
    }

    private static ReportTest TestOf(SelectedTest test) =>
        new()
        {
            Display = test.Test.FullyQualifiedName,
            Id = test.Test.Id.ToString(),
            Rules = [.. test.Rules.Select(rule => Kebab(rule.ToString())).Order(StringComparer.Ordinal)],
            Changes = test.Changes,
            PathClass = test.Class?.ToString().ToLowerInvariant(),
            Paths = test.Paths is null
                ? null
                : [.. test.Paths.Select(path => (IReadOnlyList<string>)[.. path.Select(hop => hop.ToString())])],
        };

    /// <summary>Sorted by code, then by the locator its data carries. The documented key.</summary>
    private static IReadOnlyList<ReportNotice> NoticesOf(SelectRun run) =>
    [
        .. run.Notices
            .Select(notice => new ReportNotice
            {
                Code = notice.Code,
                Kind = Kebab(notice.Kind.ToString()),
                Message = notice.Message,
                Data = notice.Data is null
                    ? null
                    : new SortedDictionary<string, object?>(
                        notice.Data.ToDictionary(entry => entry.Key, entry => entry.Value),
                        StringComparer.Ordinal),
            })
            .OrderBy(notice => notice.Code, StringComparer.Ordinal)
            .ThenBy(Locator, StringComparer.Ordinal)
    ];

    private static string Locator(ReportNotice notice) =>
        notice.Data is null ? string.Empty : string.Join(",", notice.Data.Keys);

    /// <summary><c>NothingSelected</c> becomes <c>nothing-selected</c>. Wire names are kebab-case.</summary>
    private static string Kebab(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 4);

        for (var index = 0; index < name.Length; index++)
        {
            if (index > 0 && char.IsUpper(name[index]))
            {
                builder.Append('-');
            }

            builder.Append(char.ToLowerInvariant(name[index]));
        }

        return builder.ToString();
    }
}
