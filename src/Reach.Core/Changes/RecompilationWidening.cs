using Reach.Projects;

namespace Reach.Changes;

/// <summary>
/// Widens every assembly instance that transitively references a changed one, because the
/// compiler bakes values and binding decisions into consumers.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Why the declaring side cannot cover it.</strong> After a <c>const</c> is inlined the
/// consumer's source is byte-identical, so nothing puts it in the changed set; its IL is
/// different, because the literal changed; and its IL may no longer reference the declaring
/// assembly <em>at all</em>, since an inlined constant leaves no trace of where it came from.
/// Whole-assembly widening on the declaring assembly does not reach a test that exercises the
/// consumer. The same shape covers a removed overload: an untouched call site in another
/// assembly rebinds, and its source never changed.
/// </para>
/// <para>
/// <strong>Additions trigger nothing</strong>, and that is exactly what makes so narrow a
/// trigger sufficient: an added member <em>is</em> in the changed set and the rebound call
/// site's recompiled IL points at it, so the reverse walk finds it for free. A changed signature
/// decomposes into a removal plus an addition, and the removal half fires.
/// </para>
/// <para>
/// <strong>Two narrowings were considered and rejected.</strong> Restricting to public surface
/// founders on <c>InternalsVisibleTo</c> and internal consts consumed by friend assemblies —
/// detecting the friend relationship correctly costs more than the widening saves. Including
/// attribute-argument changes is unnecessary: an attribute argument lands in the declaring
/// assembly's own metadata and is not inlined into consumers, so the declaration-level hash
/// already covers it.
/// </para>
/// <para>
/// This makes the project graph load-bearing for <em>correctness</em>, not only for scope. A bug
/// in it is now an under-selection bug.
/// </para>
/// </remarks>
internal static class RecompilationWidening
{
    /// <summary>One change that widened its consumers, and which consumers.</summary>
    /// <param name="Trigger">The member that caused it, for the forward change list.</param>
    internal sealed record Widened(
        ChangedMember Trigger,
        string Reason,
        IReadOnlyList<ProjectFile> Consumers);

    /// <summary>
    /// The consumers of every changed member that triggers. Only two things do: a removed
    /// member of any kind, and a changed compile-time constant.
    /// </summary>
    internal static IReadOnlyList<Widened> From(
        ChangedSet changed,
        AnalysisScope scope,
        Func<ChangedMember, ProjectFile?> declaringProject)
    {
        var widened = new List<Widened>();
        var referencers = Referencers(scope);

        foreach (var member in changed.Members)
        {
            var reason = ReasonFor(member);

            if (reason is null)
            {
                continue;
            }

            var declaring = declaringProject(member);

            if (declaring is null)
            {
                continue;
            }

            var consumers = referencers.GetValueOrDefault(declaring.Path) ?? [];

            if (consumers.Count > 0)
            {
                widened.Add(new Widened(member, reason, consumers));
            }
        }

        return widened;
    }

    private static string? ReasonFor(ChangedMember member)
    {
        if (member.Change == MemberChange.Removed)
        {
            return "a member was removed, and an untouched call site in a consumer rebinds without "
                + "its source changing";
        }

        if (member.ChangesCompileTimeConstant && member.Change == MemberChange.Modified)
        {
            return "a compile-time constant changed, and the compiler bakes it into every consumer "
                + "— whose source is then byte-identical while its IL is not";
        }

        return null;
    }

    /// <summary>
    /// Project path to every in-scope project that transitively references it, through the
    /// <em>project</em> graph — dozens of nodes, not millions.
    /// </summary>
    private static Dictionary<string, List<ProjectFile>> Referencers(AnalysisScope scope)
    {
        var direct = new Dictionary<string, List<ProjectFile>>(Paths.Comparer);

        foreach (var project in scope.Projects)
        {
            foreach (var reference in project.References)
            {
                Bucket(direct, Paths.Normalise(reference.Path)).Add(project);
            }
        }

        var transitive = new Dictionary<string, List<ProjectFile>>(Paths.Comparer);

        foreach (var project in scope.Projects)
        {
            var reached = new Dictionary<string, ProjectFile>(Paths.Comparer);
            var pending = new Queue<string>([project.Path]);

            while (pending.TryDequeue(out var path))
            {
                foreach (var consumer in direct.GetValueOrDefault(path) ?? [])
                {
                    if (reached.TryAdd(consumer.Path, consumer))
                    {
                        pending.Enqueue(consumer.Path);
                    }
                }
            }

            transitive[project.Path] = [.. reached.Values.OrderBy(p => p.Path, StringComparer.Ordinal)];
        }

        return transitive;
    }

    private static List<T> Bucket<T>(Dictionary<string, List<T>> index, string key)
    {
        if (!index.TryGetValue(key, out var bucket))
        {
            index[key] = bucket = [];
        }

        return bucket;
    }
}
