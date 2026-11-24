using System.Reflection.Metadata.Ecma335;
using Reach.Changes;
using Reach.Graph;
using Reach.Join;
using Reach.Projects;
using Reach.Reporting;
using static Reach.Buckets;

namespace Reach.Selection;

/// <summary>
/// Turns a changed set into the changes the walk runs over, expanding each widening into the
/// methods it covers.
/// </summary>
/// <remarks>
/// The expansion happens here and goes no further: a whole-assembly widening is one
/// <see cref="ChangeEntry"/> whatever it expands to, which is what keeps the report's forward
/// change list diff-sized rather than graph-sized.
/// </remarks>
internal sealed class RootSets
{
    private readonly Dictionary<string, List<MethodId>> _byAssembly = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MethodId>> _byType = new(StringComparer.Ordinal);

    internal RootSets(IEnumerable<GraphAssembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var reader = assembly.Reader;
            var methods = Bucket(_byAssembly, assembly.Name);

            foreach (var handle in reader.MethodDefinitions)
            {
                var id = MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle));
                var declaringType = MetadataNames.FullNameOf(
                    reader,
                    reader.GetMethodDefinition(handle).GetDeclaringType());

                methods.Add(id);
                Bucket(_byType, declaringType).Add(id);
            }
        }
    }

    /// <summary>
    /// Every method of an assembly, by its simple name. Several instances contribute together.
    /// </summary>
    internal IReadOnlyList<MethodId> InAssembly(string name) =>
        _byAssembly.GetValueOrDefault(name) ?? [];

    /// <summary>
    /// Every method of a type, including the compiler-generated members nested inside it — a
    /// state machine lives in <c>Outer+&lt;M&gt;d__0</c>, and whole-type widening has to cover
    /// the code that actually runs.
    /// </summary>
    internal IReadOnlyList<MethodId> InType(string name)
    {
        var found = new List<MethodId>(_byType.GetValueOrDefault(name) ?? []);

        foreach (var (candidate, methods) in _byType)
        {
            if (candidate.Length > name.Length
                && candidate.StartsWith(name, StringComparison.Ordinal)
                && candidate[name.Length] == '+')
            {
                found.AddRange(methods);
            }
        }

        return found;
    }

    /// <summary>
    /// The changes the walk runs over, in a stable order, and a <c>recompilation-widening</c>
    /// notice for each recompilation widening among them.
    /// </summary>
    internal RootedChanges ChangesFrom(
        ChangedSet changed,
        IReadOnlyList<JoinResult> joined,
        AnalysisScope scope,
        string repositoryRoot)
    {
        var ladder = new TierLadder(scope, repositoryRoot);
        var changes = new List<Change>();
        var notices = new List<Notice>();

        ProjectFile? ProjectContaining(string path) => scope.ProjectContaining(repositoryRoot, path);

        // A change in a file no in-scope project contains says so, whatever tier routed it, and
        // a change in a generator widens its consumers too.
        Change Widened(ChangeEntry entry, string path, IReadOnlyList<MethodId> roots) =>
            ThroughGenerator(new Change(Scoped(entry, ProjectContaining(path) is not null), roots), path, ladder);

        foreach (var result in joined.OrderBy(result => result.Member.ToString(), StringComparer.Ordinal))
        {
            if (result.Joined)
            {
                // The one place ChangeTier.Member is constructed, which is what lets the walk
                // read the tier to tell "these roots are the declaration" from "these roots
                // are an expansion".
                changes.Add(ThroughGenerator(
                    new Change(
                        Entry(changes.Count, result.Member.ToString(), ChangeTier.Member, "the member changed"),
                        result.Methods),
                    result.Member.Path,
                    ladder));

                continue;
            }

            // A removed member no longer exists, so it can never join. Its declaring type's
            // whole-type widening, added below, is the deliberate response to a deletion, and
            // recompilation widening still covers the assemblies that reference this one.
            if (WidenedForDeletion(changed, result.Member))
            {
                continue;
            }

            // A declaration that fails to join falls through to whole-assembly widening for its
            // project. Over-selection, and it is what makes a phantom member from a misparse
            // survivable rather than a silent hole.
            var project = ProjectContaining(result.Member.Path);

            changes.Add(Widened(
                Entry(
                    changes.Count,
                    result.Member.ToString(),
                    ChangeTier.WholeAssembly,
                    "the declaration could not be matched to compiled code"),
                result.Member.Path,
                project is null ? [] : InAssembly(project.AssemblyName)));
        }

        foreach (var widening in changed.TypeWidenings.OrderBy(w => w.DeclaringType, StringComparer.Ordinal))
        {
            changes.Add(Widened(
                Entry(changes.Count, widening.DeclaringType, ChangeTier.WholeType, Describe(widening.Reason)),
                widening.Path,
                InType(widening.DeclaringType)));
        }

        foreach (var widening in changed.AssemblyWidenings.OrderBy(w => w.DeclaringType, StringComparer.Ordinal))
        {
            changes.Add(Widened(
                Entry(changes.Count, widening.DeclaringType, ChangeTier.WholeAssembly, Describe(widening.Reason)),
                widening.Path,
                widening.Project is null ? [] : InAssembly(widening.Project.AssemblyName)));
        }

        // The blast radius is real and accepted because it is legible: each one is its own
        // entry, so a pull request that selected everything shows exactly which const did it.
        foreach (var widened in RecompilationWidening.From(changed, scope, member => ProjectContaining(member.Path)))
        {
            changes.Add(new Change(
                Entry(
                    changes.Count,
                    widened.Trigger.ToString(),
                    ChangeTier.WholeAssembly,
                    widened.Reason),
                [.. widened.Consumers.SelectMany(project => InAssembly(project.AssemblyName)).Distinct()]));

            notices.Add(widened.ToNotice());
        }

        foreach (var routed in ladder.RouteAll(
            changed.UnmappablePaths.OrderBy(path => path.Path, StringComparer.Ordinal)))
        {
            changes.Add(new Change(
                Entry(changes.Count, routed.Path.Path, ChangeTier.WholeAssembly, routed.Rule.Description),
                [.. routed.Projects.SelectMany(project => InAssembly(project.AssemblyName)).Distinct()]));
        }

        return new RootedChanges(changes, notices);
    }

    /// <summary>
    /// A change in a generator project, re-routed to rule 4: the generator's consumers widen
    /// whole, as they do when its project file changes. The change's own roots are kept, so a
    /// generator that is also referenced normally loses nothing.
    /// </summary>
    /// <remarks>
    /// It never meets an entry <see cref="Scoped"/> marked outside the analysis scope: both ask
    /// <see cref="AnalysisScope.ProjectContaining(string, string)"/>, and a generator is recognised only as an
    /// in-scope project. A generator outside the scope has no in-scope consumer, so nothing it
    /// does can reach a test, and its change is reported as outside the scope.
    /// </remarks>
    private Change ThroughGenerator(Change change, string repositoryRelativePath, TierLadder ladder)
    {
        if (ladder.ConsumersOfGeneratorContaining(repositoryRelativePath) is not { } routed)
        {
            return change;
        }

        return new Change(
            change.Entry with { Tier = ChangeTier.WholeAssembly, Reason = routed.Rule.Description },
            [
                .. change.Roots
                    .Concat(routed.Projects.SelectMany(project => InAssembly(project.AssemblyName)))
                    .Distinct()
            ]);
    }

    /// <summary>
    /// Whether <paramref name="member"/> is a removal its declaring type's deletion widening
    /// already covers. Anything else that fails to join keeps whole-assembly widening.
    /// </summary>
    private static bool WidenedForDeletion(ChangedSet changed, ChangedMember member) =>
        member.Change == MemberChange.Removed
        && changed.TypeWidenings.Any(widening =>
            widening.Reason == WideningReason.DeletedMember
            && string.Equals(widening.DeclaringType, member.DeclaringType, StringComparison.Ordinal));

    private static ChangeEntry Entry(int index, string display, ChangeTier tier, string reason) =>
        new(index, display, tier, reason);

    /// <summary>
    /// A change in a file no in-scope project contains says so. Only the report reads this: the
    /// change's roots are computed exactly as they were.
    /// </summary>
    private static ChangeEntry Scoped(ChangeEntry entry, bool inScope) =>
        inScope
            ? entry
            : entry with
            {
                Reason = "the file is in no project a test project references, so it is outside the analysis scope",
                OutsideAnalysisScope = true,
            };

    private static string Describe(WideningReason reason) => reason switch
    {
        WideningReason.DeletedMember =>
            "a member was deleted, and a deletion can rebind a call rather than remove it",
        WideningReason.TypeHeaderChanged => "the type's header changed",
        WideningReason.TypeMoved => "the type moved between files",
        _ => "the type has no identity in the current binaries",
    };
}

/// <summary>
/// One change, and every method the walk should start from for it.
/// </summary>
/// <remarks>
/// Whether the roots <em>are</em> the changed declaration is not carried separately: it is
/// exactly <c>Entry.Tier == ChangeTier.Member</c>, and two fields that must agree forever is one
/// more than the fact needs.
/// </remarks>
internal sealed record Change(ChangeEntry Entry, IReadOnlyList<MethodId> Roots);

/// <summary>
/// The changes the walk runs over, and the notices working them out raised.
/// </summary>
internal sealed record RootedChanges(IReadOnlyList<Change> Changes, IReadOnlyList<Notice> Notices);
