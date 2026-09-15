using System.Text.Json;

namespace Reach.Projects;

/// <summary>
/// Turns a directory or a path into the thing Reach analyses. Current directory only, never
/// recursive: a monorepo with six solutions would get a coin flip, and the wrong solution
/// silently produces a wrong analysis scope.
/// </summary>
/// <remarks>
/// This deliberately diverges from MSBuild, which compares base names and errors when they
/// differ. For a build a solution and a project are two ways of naming work; for Reach they
/// are two different analysis scopes, so choosing the project is choosing the <em>narrower</em>
/// one — a silent under-selection with a plausible-looking report. Do not "fix" it to match.
/// </remarks>
internal static class TargetDiscovery
{
    private static readonly string[] SolutionExtensions = [".sln", ".slnx"];
    private static readonly string[] ProjectExtensions = [".csproj", ".fsproj", ".vbproj"];

    private const string FilterExtension = ".slnf";

    internal static TargetDiscoveryResult Discover(string path)
    {
        var full = Path.GetFullPath(path);

        if (Directory.Exists(full))
        {
            return InDirectory(full);
        }

        if (!File.Exists(full))
        {
            return TargetDiscoveryResult.Ambiguous($"'{path}' is neither a file nor a directory.");
        }

        var extension = Path.GetExtension(full);

        if (extension.Equals(FilterExtension, StringComparison.OrdinalIgnoreCase))
        {
            return RefuseFilter(full);
        }

        if (SolutionExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return TargetDiscoveryResult.Found(full, TargetKind.Solution);
        }

        if (ProjectExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return TargetDiscoveryResult.Found(full, TargetKind.Project);
        }

        return TargetDiscoveryResult.Ambiguous(
            $"'{path}' is not a solution or a project. Expected one of "
            + $"{string.Join(", ", SolutionExtensions.Concat(ProjectExtensions))}.");
    }

    private static TargetDiscoveryResult InDirectory(string directory)
    {
        var solutions = FilesWith(directory, SolutionExtensions);

        // Exactly one solution wins, whatever projects sit beside it.
        if (solutions.Length == 1)
        {
            return TargetDiscoveryResult.Found(solutions[0], TargetKind.Solution);
        }

        if (solutions.Length > 1)
        {
            return TargetDiscoveryResult.Ambiguous(
                $"{directory} holds more than one solution: {Names(solutions)}. "
                + "Name the one to analyse.");
        }

        // Only once no solution is present: a filter beside its own solution is a convenience,
        // but a filter *as the target* states an intent Reach would otherwise ignore silently.
        var filters = FilesWith(directory, [FilterExtension]);

        if (filters.Length > 0)
        {
            return RefuseFilter(filters[0]);
        }

        var projects = FilesWith(directory, ProjectExtensions);

        if (projects.Length == 1)
        {
            return TargetDiscoveryResult.Found(projects[0], TargetKind.Project);
        }

        return TargetDiscoveryResult.Ambiguous(
            projects.Length == 0
                ? $"{directory} holds no solution and no project."
                : $"{directory} holds no solution and more than one project: {Names(projects)}. "
                    + "Name the one to analyse.");
    }

    /// <summary>
    /// A solution filter declares a subset, and honouring it would shrink analysis scope by
    /// exactly the mechanism ADR-0002 forbids. Rejected rather than expanded — and the
    /// message names the underlying solution, because that is the argument the caller wants.
    /// </summary>
    private static TargetDiscoveryResult RefuseFilter(string filterPath)
    {
        var solution = UnderlyingSolution(filterPath);

        return TargetDiscoveryResult.Ambiguous(
            $"{Path.GetFileName(filterPath)} is a solution filter, and a filter declares a "
            + "subset of the solution. Analysing the subset would narrow the analysis scope "
            + "and under-select. "
            + (solution is null
                ? "Point Reach at the solution instead."
                : $"Point Reach at {solution} instead."));
    }

    /// <summary>Read only far enough to name the solution. Never expanded into a project list.</summary>
    private static string? UnderlyingSolution(string filterPath)
    {
        try
        {
            using var stream = File.OpenRead(filterPath);
            using var document = JsonDocument.Parse(stream);

            return document.RootElement.TryGetProperty("solution", out var solution)
                && solution.TryGetProperty("path", out var path)
                    ? path.GetString()?.Replace('\\', Path.DirectorySeparatorChar)
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static string[] FilesWith(string directory, string[] extensions) =>
        [.. Directory
            .EnumerateFiles(directory)
            .Where(file => extensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.Ordinal)];

    private static string Names(IEnumerable<string> paths) =>
        string.Join(", ", paths.Select(Path.GetFileName));
}
