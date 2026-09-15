using Microsoft.VisualStudio.SolutionPersistence.Serializer;

namespace Reach.Projects;

/// <summary>
/// Reads a solution's project list with <c>Microsoft.VisualStudio.SolutionPersistence</c> —
/// what <c>dotnet sln</c>, <c>Microsoft.Build.dll</c> and NuGet.Client all use, so Reach's
/// reading of a solution agrees with the build's by construction. One API reads both
/// <c>.sln</c> and <c>.slnx</c>, which matters because <c>.slnx</c> is the default for
/// <c>dotnet new sln</c> on .NET 10.
/// </summary>
/// <remarks>
/// <c>dotnet sln list</c> was rejected: no JSON output, a localised header, paths only, and it
/// fails outright when a directory holds both <c>Foo.sln</c> and <c>Foo.slnx</c> — exactly
/// what <c>dotnet sln migrate</c> leaves behind. The serializer never opens the referenced
/// project files, which is what <c>--no-build</c> needs.
/// </remarks>
internal static class SolutionReader
{
    /// <summary>
    /// Absolute paths to the solution's C#-family projects. Non-project entries — solution
    /// folders, and project types Reach cannot read — are left out rather than guessed at.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> ReadProjectPathsAsync(
        string solutionPath,
        CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(solutionPath);
        var serializer = SolutionSerializers.GetSerializerByMoniker(full);

        if (serializer is null)
        {
            return [];
        }

        var model = await serializer.OpenAsync(full, cancellationToken).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(full)!;

        return
        [
            .. model.SolutionProjects
                // Type is empty for a plain .csproj and the type GUID differs between .sln
                // and .slnx for the same project, so Extension is the only stable switch.
                .Where(project => IsReadable(project.Extension))
                .Select(project => Resolve(directory, project.FilePath))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.Ordinal)
        ];
    }

    private static bool IsReadable(string extension) =>
        extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>.sln</c> paths are neither canonicalised nor separator-normalised, and a solution
    /// written on Windows carries backslashes wherever it is read.
    /// </summary>
    private static string Resolve(string solutionDirectory, string projectPath) =>
        Path.GetFullPath(
            Path.Combine(solutionDirectory, projectPath.Replace('\\', Path.DirectorySeparatorChar)));
}
