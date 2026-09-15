using Reach.Assemblies;
using Reach.Projects;
using Reach.Reporting;

namespace Reach.Changes;

/// <summary>Which rule routed an unmappable change, and which way it errs.</summary>
/// <param name="Row">The rule table's own numbering, so a report line can be looked up.</param>
internal sealed record RoutingRule(int Row, string Description, bool ErrsUnder = false);

/// <summary>One unmappable change, routed.</summary>
/// <param name="Projects">The in-scope projects whose assemblies widen. Empty is a valid answer.</param>
internal sealed record RoutedChange(
    ChangedPath Path,
    RoutingRule Rule,
    IReadOnlyList<ProjectFile> Projects);

/// <summary>
/// Routes every change that does not map to a member, without letting the table degenerate into
/// "select everything".
/// </summary>
/// <remarks>
/// <para>
/// <strong>Routing is per change, not per file, and the results union.</strong> Per-file gating
/// is the only version that can return less than the sum of its parts, and the case it loses is
/// the ordinary refactoring commit: a deletion sharing a file with an edit, where the edit keeps
/// the file on tier 1 and the deletion resolves to nothing.
/// </para>
/// <para>
/// <strong>No row adds dependents</strong>, and that looks like an omission so it is worth
/// knowing why: whole-assembly widening puts every method of the assembly into the changed set,
/// and reverse reachability then walks <em>backwards</em>, so everything downstream is already
/// reached. Recompilation widening is the exception, and only because inlining can erase the
/// reference the reverse walk would have followed.
/// </para>
/// </remarks>
internal sealed class TierLadder(AnalysisScope scope, string repositoryRoot)
{
    private static readonly RoutingRule Project = new(1, "a project file changed");

    private static readonly RoutingRule DirectoryScoped =
        new(2, "a build file changed that applies to every project at or below its directory");

    private static readonly RoutingRule SolutionWide = new(3, "a solution-wide build file changed");

    private static readonly RoutingRule Generator = new(4, "a source generator or analyser project changed");

    private static readonly RoutingRule Content = new(5, "content copied to the output changed");

    private static readonly RoutingRule NearestProject =
        new(6, "no rule matched, so the nearest ancestor project was used");

    /// <summary>The one rule in Reach that errs toward selecting nothing, taken as an owner decision.</summary>
    private static readonly RoutingRule Unattributed =
        new(6, "no rule matched and no project contains it", ErrsUnder: true);

    /// <summary>
    /// Applies to every in-scope project. These files are genuinely solution-wide in effect,
    /// which is why row 3 is the only row that is.
    /// </summary>
    private static readonly string[] SolutionWideNames =
    [
        "global.json",
        "nuget.config",
        "packages.lock.json",
        "directory.packages.props",
    ];

    /// <summary>
    /// Applies to every project at or below the file's own directory. MSBuild's own discovery
    /// walks <em>up</em> from each project, so containment is not a heuristic — it is the same
    /// rule the build itself uses.
    /// </summary>
    private static readonly string[] DirectoryScopedNames =
    [
        "directory.build.props",
        "directory.build.targets",
        ".editorconfig",
    ];

    private static readonly string[] SolutionExtensions = [".sln", ".slnx", ".slnf"];

    internal RoutedChange Route(ChangedPath path)
    {
        var name = System.IO.Path.GetFileName(path.Path).ToLowerInvariant();
        var extension = System.IO.Path.GetExtension(path.Path).ToLowerInvariant();

        if (extension is ".csproj" or ".fsproj" or ".vbproj")
        {
            var project = ProjectAt(path.Path);

            // A generator project is consumed rather than executed, so what widens is every
            // project that references it — not the generator itself, whose output lands nowhere
            // Reach scans.
            if (project is not null && IsGenerator(project))
            {
                return new RoutedChange(path, Generator, ConsumersOf(project));
            }

            return new RoutedChange(path, Project, project is null ? [] : [project]);
        }

        if (SolutionExtensions.Contains(extension, StringComparer.Ordinal)
            || SolutionWideNames.Contains(name, StringComparer.Ordinal))
        {
            return new RoutedChange(path, SolutionWide, scope.Projects);
        }

        if (DirectoryScopedNames.Contains(name, StringComparer.Ordinal))
        {
            return new RoutedChange(path, DirectoryScoped, AtOrBelow(DirectoryOf(path.Path)));
        }

        var containing = NearestAncestorProject(path.Path);

        if (containing is null)
        {
            // Rows 1–5 enumerate every build-affecting file that lives at a repository root, so
            // the residue genuinely is documentation-shaped: README.md, .gitignore, CI YAML,
            // licence files. The alternative — whole-solution selection for any unrecognised
            // file — means a README change runs the entire suite, which is not conservatism
            // with a cost but a tool nobody keeps switched on.
            return new RoutedChange(path, Unattributed, []);
        }

        return new RoutedChange(
            path,
            IsContent(extension) ? Content : NearestProject,
            [containing]);
    }

    internal IReadOnlyList<RoutedChange> RouteAll(IEnumerable<ChangedPath> paths) =>
        [.. paths.Select(Route)];

    /// <summary>
    /// Tier 2: a changed source file with no changed member, but which some assembly's debug
    /// symbols point at. Every such assembly widens.
    /// </summary>
    /// <remarks>
    /// The inverse of the correspondence check's document enumeration — built once from the
    /// verdicts it already produced, never by re-enumerating.
    /// </remarks>
    internal static IReadOnlyList<AssemblyInstance> AssembliesListing(
        string absolutePath,
        IEnumerable<AssemblyInstance> instances) =>
    [
        .. instances.Where(instance =>
            instance.Assembly.Documents.Any(document => Paths.Same(
                Paths.Normalise(document.Path),
                Paths.Normalise(absolutePath))))
    ];

    /// <summary>
    /// Fires only when row 6 matched with no ancestor project, so the notice is precise rather
    /// than a blanket disclaimer.
    /// </summary>
    internal static Notice? UnattributedNotice(IEnumerable<RoutedChange> routed)
    {
        var orphans = routed
            .Where(change => change.Rule == Unattributed)
            .Select(change => change.Path.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (orphans.Length == 0)
        {
            return null;
        }

        return new Notice(
            NoticeCodes.UnmappedFileNoProject,
            NoticeKind.BlindSpot,
            $"{orphans.Length} changed file(s) matched no rule and sit inside no project, so "
            + "nothing was selected for them: " + string.Join(", ", orphans),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["paths"] = orphans });
    }

    /// <summary>
    /// Conventional, and it says so when it fails. <c>OutputItemType="Analyzer"</c> is a
    /// convention rather than a prescription — the Roslyn cookbooks contain zero occurrences of
    /// it — so the markers are checked and nothing is inferred when none matches.
    /// </summary>
    private bool IsGenerator(ProjectFile project) =>
        scope.Projects.Any(consumer => consumer.References.Any(reference =>
            reference.IsAnalyzer && Paths.Same(Paths.Normalise(reference.Path), project.Path)));

    private IReadOnlyList<ProjectFile> ConsumersOf(ProjectFile generator) =>
    [
        .. scope.Projects.Where(consumer => consumer.References.Any(reference =>
            Paths.Same(Paths.Normalise(reference.Path), generator.Path)))
    ];

    private ProjectFile? ProjectAt(string repositoryRelativePath) =>
        scope.Projects.FirstOrDefault(project => Paths.Same(project.Path, Absolute(repositoryRelativePath)));

    private IReadOnlyList<ProjectFile> AtOrBelow(string directory) =>
        [.. scope.Projects.Where(project => Paths.IsUnder(project.Directory, directory))];

    /// <summary>
    /// The nearest ancestor project directory, which reproduces the SDK's default
    /// <c>**/*.cs</c> globbing without running MSBuild.
    /// </summary>
    private ProjectFile? NearestAncestorProject(string repositoryRelativePath)
    {
        var full = Absolute(repositoryRelativePath);

        return scope.Projects
            .Where(project => Paths.IsUnder(full, project.Directory))
            .OrderByDescending(project => project.Directory.Length)
            .FirstOrDefault();
    }

    private static bool IsContent(string extension) =>
        extension is ".json" or ".resx" or ".xml" or ".config" or ".txt" or ".yaml" or ".yml";

    private string DirectoryOf(string repositoryRelativePath) =>
        System.IO.Path.GetDirectoryName(Absolute(repositoryRelativePath)) ?? repositoryRoot;

    private string Absolute(string repositoryRelativePath) =>
        Paths.Normalise(System.IO.Path.Combine(repositoryRoot, repositoryRelativePath));
}
