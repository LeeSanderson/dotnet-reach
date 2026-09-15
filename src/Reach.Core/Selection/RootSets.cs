using System.Reflection.Metadata.Ecma335;
using Reach.Changes;
using Reach.Graph;
using Reach.Join;
using Reach.Projects;

namespace Reach.Selection;

/// <summary>One change, and every method the walk should start from for it.</summary>
/// <param name="RootsAreTheDeclaration">
/// True when the roots <em>are</em> the changed declaration, which is the member tier. A test
/// among them is selected by <c>own-source-changed</c> rather than by having walked anywhere, so
/// the walk must not also claim it.
/// <para>
/// False for every widening, where the roots are an expansion rather than the declaration. There
/// is no <c>own-source-changed</c> attribution for those, so excluding them would drop a test the
/// widening is supposed to catch — under-selection, out of a rule meant only to keep two
/// attributions from overlapping.
/// </para>
/// </param>
internal sealed record Change(
    ChangeEntry Entry,
    IReadOnlyList<MethodId> Roots,
    bool RootsAreTheDeclaration = false);

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
    private readonly Dictionary<string, List<MethodId>> byAssembly = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MethodId>> byType = new(StringComparer.Ordinal);

    internal RootSets(IEnumerable<GraphAssembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            var reader = assembly.Reader;
            var methods = Bucket(byAssembly, assembly.Name);

            foreach (var handle in reader.MethodDefinitions)
            {
                var id = MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle));
                var declaringType = MetadataNames.FullNameOf(
                    reader,
                    reader.GetMethodDefinition(handle).GetDeclaringType());

                methods.Add(id);
                Bucket(byType, declaringType).Add(id);
            }
        }
    }

    /// <summary>Every method of an assembly, by its simple name. Several instances contribute together.</summary>
    internal IReadOnlyList<MethodId> InAssembly(string name) =>
        byAssembly.GetValueOrDefault(name) ?? [];

    /// <summary>
    /// Every method of a type, including the compiler-generated members nested inside it — a
    /// state machine lives in <c>Outer+&lt;M&gt;d__0</c>, and whole-type widening has to cover
    /// the code that actually runs.
    /// </summary>
    internal IReadOnlyList<MethodId> InType(string name)
    {
        var found = new List<MethodId>(byType.GetValueOrDefault(name) ?? []);

        foreach (var (candidate, methods) in byType)
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

    /// <summary>The changes the walk runs over, in a stable order.</summary>
    internal IReadOnlyList<Change> ChangesFrom(
        ChangedSet changed,
        IReadOnlyList<JoinResult> joined,
        AnalysisScope scope,
        TierLadder ladder)
    {
        var changes = new List<Change>();

        foreach (var result in joined.OrderBy(result => result.Member.ToString(), StringComparer.Ordinal))
        {
            if (result.Joined)
            {
                changes.Add(new Change(
                    Entry(changes.Count, result.Member.ToString(), ChangeTier.Member, "the member changed"),
                    result.Methods,
                    RootsAreTheDeclaration: true));

                continue;
            }

            // A declaration that fails to join falls through to whole-assembly widening for its
            // project. Over-selection, and it is what makes a phantom member from a misparse
            // survivable rather than a silent hole.
            var project = ProjectFor(scope, result.Member.Path);

            changes.Add(new Change(
                Entry(
                    changes.Count,
                    result.Member.ToString(),
                    ChangeTier.WholeAssembly,
                    "the declaration could not be matched to compiled code"),
                project is null ? [] : InAssembly(project.AssemblyName)));
        }

        foreach (var widening in changed.TypeWidenings.OrderBy(w => w.DeclaringType, StringComparer.Ordinal))
        {
            changes.Add(new Change(
                Entry(changes.Count, widening.DeclaringType, ChangeTier.WholeType, Describe(widening.Reason)),
                InType(widening.DeclaringType)));
        }

        foreach (var widening in changed.AssemblyWidenings.OrderBy(w => w.DeclaringType, StringComparer.Ordinal))
        {
            changes.Add(new Change(
                Entry(changes.Count, widening.DeclaringType, ChangeTier.WholeAssembly, Describe(widening.Reason)),
                widening.Project is null ? [] : InAssembly(widening.Project.AssemblyName)));
        }

        // The blast radius is real and accepted because it is legible: each one is its own
        // entry, so a pull request that selected everything shows exactly which const did it.
        foreach (var widened in RecompilationWidening.From(
            changed,
            scope,
            member => ProjectFor(scope, member.Path)))
        {
            changes.Add(new Change(
                Entry(
                    changes.Count,
                    widened.Trigger.ToString(),
                    ChangeTier.WholeAssembly,
                    widened.Reason),
                [.. widened.Consumers.SelectMany(project => InAssembly(project.AssemblyName)).Distinct()]));
        }

        foreach (var routed in ladder.RouteAll(
            changed.UnmappablePaths.OrderBy(path => path.Path, StringComparer.Ordinal)))
        {
            changes.Add(new Change(
                Entry(changes.Count, routed.Path.Path, ChangeTier.WholeAssembly, routed.Rule.Description),
                [.. routed.Projects.SelectMany(project => InAssembly(project.AssemblyName)).Distinct()]));
        }

        return changes;
    }

    private static ChangeEntry Entry(int index, string display, ChangeTier tier, string reason) =>
        new(index, display, tier, reason);

    private static string Describe(WideningReason reason) => reason switch
    {
        WideningReason.DeletedMember =>
            "a member was deleted, and a deletion can rebind a call rather than remove it",
        WideningReason.TypeHeaderChanged => "the type's header changed",
        WideningReason.TypeMoved => "the type moved between files",
        _ => "the type has no identity in the current binaries",
    };

    /// <summary>
    /// The nearest ancestor project directory, which reproduces the SDK's default
    /// <c>**/*.cs</c> globbing without running MSBuild.
    /// </summary>
    private static ProjectFile? ProjectFor(AnalysisScope scope, string repositoryRelativePath) =>
        scope.Projects
            .Where(project => repositoryRelativePath.Replace('\\', '/')
                .StartsWith(RelativeDirectoryOf(scope, project), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(project => project.Directory.Length)
            .FirstOrDefault();

    /// <summary>
    /// Compared as repository-relative forward-slashed prefixes, because that is the spelling
    /// git returns and the only one both sides share.
    /// </summary>
    private static string RelativeDirectoryOf(AnalysisScope scope, ProjectFile project)
    {
        var root = CommonRoot(scope);
        var relative = Path.GetRelativePath(root, project.Directory).Replace('\\', '/');

        return relative == "." ? string.Empty : relative + "/";
    }

    private static string CommonRoot(AnalysisScope scope)
    {
        var directories = scope.Projects.Select(project => project.Directory).ToArray();

        if (directories.Length == 0)
        {
            return string.Empty;
        }

        var root = directories[0];

        while (!directories.All(directory => Paths.IsUnder(directory, root)))
        {
            var parent = Path.GetDirectoryName(root);

            if (parent is null)
            {
                return root;
            }

            root = parent;
        }

        return root;
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
