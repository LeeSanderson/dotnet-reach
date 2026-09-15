using Reach.Projects;

namespace Reach.Assemblies;

/// <summary>One project compiled for one target framework, matched to the file that holds it.</summary>
internal sealed record AssemblyInstance(ExpectedAssemblyInstance Expected, ScannedAssembly Assembly)
{
    internal string Name => Expected.Project.AssemblyName;
}

/// <summary>
/// What discovery concluded. Absence is unambiguous here in a way it is not for a predicted
/// path: the tree was searched.
/// </summary>
internal sealed record AssemblyDiscoveryResult(
    IReadOnlyList<AssemblyInstance> Instances,
    string Message)
{
    internal bool Succeeded => Message.Length == 0;

    internal ExitCode ExitCode => Succeeded ? ExitCode.Success : ExitCode.AssemblyDiscoveryFailed;
}

/// <summary>Matches the scan's results to the expected assembly-instance set.</summary>
internal static class AssemblyDiscovery
{
    internal static AssemblyDiscoveryResult Discover(
        AnalysisScope scope,
        string root,
        string sourceRoot,
        LayoutHints hints)
    {
        var expectedNames = scope.ExpectedAssemblies
            .Select(instance => instance.Project.AssemblyName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var scanned = AssemblyScanner.Scan(root, expectedNames, sourceRoot)
            .Where(hints.Keeps)
            .ToArray();

        var instances = new List<AssemblyInstance>();
        var problems = new List<string>();

        foreach (var expected in scope.ExpectedAssemblies)
        {
            var declared = TargetFrameworkMoniker.Parse(expected.TargetFramework);

            var candidates = scanned
                .Where(assembly => assembly.IsFirstParty)
                .Where(assembly => string.Equals(
                    assembly.SimpleName,
                    expected.Project.AssemblyName,
                    StringComparison.OrdinalIgnoreCase))
                .Where(assembly => declared is not null
                    && assembly.Framework is not null
                    && declared.Matches(assembly.Framework))
                .ToArray();

            // The same assembly is routinely copied into every consuming project's output, so
            // the raw candidate count is not the question. Two files carrying one module
            // version id are one build of one assembly; two module version ids are two builds,
            // and choosing between those is the thing discovery refuses to do.
            var builds = candidates.GroupBy(assembly => assembly.ModuleVersionId).ToArray();

            if (builds.Length == 0)
            {
                problems.Add(Missing(expected, scanned));
            }
            else if (builds.Length > 1)
            {
                problems.Add(Ambiguous(expected, builds.SelectMany(build => build), hints));
            }
            else
            {
                instances.Add(new AssemblyInstance(expected, Representative(builds[0], expected)));
            }
        }

        return new AssemblyDiscoveryResult(instances, string.Join("\n\n", problems));
    }

    /// <summary>
    /// All copies are the same build, so any of them will do — but the copy under the project's
    /// own directory is the one whose path a reader recognises, and picking deterministically
    /// keeps the report byte-identical between runs.
    /// </summary>
    private static ScannedAssembly Representative(
        IEnumerable<ScannedAssembly> build,
        ExpectedAssemblyInstance expected) =>
        build
            .OrderByDescending(assembly => Paths.IsUnder(assembly.Path, expected.Project.Directory))
            .ThenBy(assembly => assembly.Path.Length)
            .ThenBy(assembly => assembly.Path, StringComparer.Ordinal)
            .First();

    private static string Missing(ExpectedAssemblyInstance expected, IReadOnlyList<ScannedAssembly> scanned)
    {
        var sameName = scanned
            .Where(assembly => string.Equals(
                assembly.SimpleName,
                expected.Project.AssemblyName,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var detail = sameName.Length == 0
            ? "Nothing of that name was found under the target's directory tree."
            : "Found, but not as that assembly instance:\n"
                + string.Join(
                    "\n",
                    sameName.Select(assembly =>
                        $"  {assembly.Path} — {Describe(assembly)}"));

        return $"No assembly was found for {expected}. {detail}";
    }

    private static string Describe(ScannedAssembly assembly) =>
        assembly.IsFirstParty
            ? $"built for {assembly.Framework?.ToString() ?? "an unreadable target framework"}"
            : "its debug symbols point at source outside the working tree, so it is not first-party";

    /// <summary>
    /// Guessing was rejected: newest-mtime silently picks a stale Release build over a fresh
    /// Debug one about as often as not, converting a loud stop into under-selection with no
    /// notice. So the message names the option that settles it instead.
    /// </summary>
    private static string Ambiguous(
        ExpectedAssemblyInstance expected,
        IEnumerable<ScannedAssembly> candidates,
        LayoutHints hints)
    {
        var listed = string.Join(
            "\n",
            candidates.Select(assembly => "  " + assembly.Path).Order(StringComparer.Ordinal));

        return $"More than one build of {expected} was found, and Reach will not choose between "
            + $"them:\n{listed}\n\n{hints.Suggestion()}";
    }
}
