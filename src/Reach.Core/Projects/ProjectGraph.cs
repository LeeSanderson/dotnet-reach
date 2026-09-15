namespace Reach.Projects;

/// <summary>
/// The dependency graph between project files, derived from their project references. Much
/// coarser than the call graph, and available without compiling anything.
/// </summary>
internal sealed class ProjectGraph
{
    private readonly Dictionary<string, ProjectFile> byPath;

    private ProjectGraph(Dictionary<string, ProjectFile> byPath) => this.byPath = byPath;

    internal IReadOnlyCollection<ProjectFile> Projects => byPath.Values;

    /// <summary>
    /// Reads the roots and everything they reach, transitively. A reference is followed even
    /// when its target sits outside the solution: the solution supplies the expected project
    /// list, never the closure.
    /// </summary>
    internal static ProjectGraph Load(IEnumerable<string> rootPaths)
    {
        var byPath = new Dictionary<string, ProjectFile>(Paths.Comparer);
        var queue = new Queue<string>(rootPaths.Select(Paths.Normalise));

        while (queue.TryDequeue(out var path))
        {
            if (byPath.ContainsKey(path) || !File.Exists(path))
            {
                continue;
            }

            var project = ProjectFileReader.Read(path);
            byPath.Add(path, project);

            foreach (var reference in project.References)
            {
                queue.Enqueue(Paths.Normalise(reference.Path));
            }
        }

        return new ProjectGraph(byPath);
    }

    internal ProjectFile? Find(string path) =>
        byPath.GetValueOrDefault(Paths.Normalise(path));

    /// <summary>
    /// <paramref name="project"/> and everything it transitively references, each once.
    /// References carrying <c>ReferenceOutputAssembly=false</c> are followed too: such a
    /// reference expresses build order rather than runtime executability, so including it
    /// errs wide, which is the safe direction.
    /// </summary>
    internal IReadOnlyList<ProjectFile> ClosureOf(ProjectFile project)
    {
        var seen = new Dictionary<string, ProjectFile>(Paths.Comparer);
        var pending = new Stack<ProjectFile>([project]);

        while (pending.TryPop(out var current))
        {
            if (!seen.TryAdd(current.Path, current))
            {
                continue;
            }

            foreach (var reference in current.References)
            {
                var referenced = Find(reference.Path);

                if (referenced is not null)
                {
                    pending.Push(referenced);
                }
            }
        }

        return [.. seen.Values.OrderBy(entry => entry.Path, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Every reference in the graph that points at <paramref name="project"/>. What decides
    /// whether a project is expected to put an assembly where Reach will look for it.
    /// </summary>
    internal IReadOnlyList<ProjectReference> ReferencesTo(ProjectFile project) =>
    [
        .. byPath.Values
            .SelectMany(candidate => candidate.References)
            .Where(reference => Paths.Same(Paths.Normalise(reference.Path), project.Path))
    ];
}
