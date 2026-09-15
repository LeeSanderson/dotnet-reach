using System.Xml.Linq;

namespace Reach.Projects;

/// <summary>
/// Reads a project file's raw XML. Not MSBuild: nothing here evaluates a condition, expands a
/// property or imports a target, because <c>--no-build</c> has to work and an MSBuild
/// reference would drag the whole engine into a tool whose job is to be cheap and offline.
/// </summary>
/// <remarks>
/// The accepted cost is that a property set by a condition Reach cannot evaluate is read
/// literally, last one wins. Ticket 07's scan-and-verify absorbs the consequence: a wrong
/// target framework here shows up as an assembly that is expected and not found, which is an
/// error rather than a silent under-selection.
/// </remarks>
internal static class ProjectFileReader
{
    private const string DirectoryBuildProps = "Directory.Build.props";

    internal static ProjectFile Read(string projectPath)
    {
        var full = Path.GetFullPath(projectPath);
        var directory = Path.GetDirectoryName(full)!;

        var project = Load(full);

        // MSBuild stops at the first Directory.Build.props walking up, and so does this.
        var inherited = NearestDirectoryBuildProps(directory);

        return new ProjectFile(
            full,
            AssemblyNameOf(full, project, inherited),
            TargetFrameworksOf(project, inherited),
            ReferencesOf(project, directory),
            [.. PackageIdsOf(project), .. PackageIdsOf(inherited)]);
    }

    private static XElement? Load(string path)
    {
        try
        {
            return XDocument.Load(path).Root;
        }
        catch (System.Xml.XmlException)
        {
            // An unparseable project file is not fatal here. Ticket 07's expected-assembly
            // check is what turns a project Reach could not read into a loud failure, and
            // ticket 16's rule table widens rather than guessing.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static XElement? NearestDirectoryBuildProps(string directory)
    {
        for (var current = directory; current is not null; current = Path.GetDirectoryName(current))
        {
            var candidate = Path.Combine(current, DirectoryBuildProps);

            if (File.Exists(candidate))
            {
                return Load(candidate);
            }
        }

        return null;
    }

    private static string AssemblyNameOf(string projectPath, XElement? project, XElement? inherited)
    {
        var declared = Property(project, "AssemblyName") ?? Property(inherited, "AssemblyName");

        // A property Reach cannot expand is worse than no property: the base name is what
        // MSBuild itself defaults to, and $(MSBuildProjectName) is overwhelmingly what such
        // an expression says anyway.
        return declared is null || declared.Contains("$(", StringComparison.Ordinal)
            ? Path.GetFileNameWithoutExtension(projectPath)
            : declared;
    }

    private static IReadOnlyList<string> TargetFrameworksOf(XElement? project, XElement? inherited)
    {
        var many = Property(project, "TargetFrameworks") ?? Property(inherited, "TargetFrameworks");

        if (many is not null)
        {
            return Split(many);
        }

        var one = Property(project, "TargetFramework") ?? Property(inherited, "TargetFramework");

        return one is null ? [] : Split(one);
    }

    private static string[] Split(string value) =>
        [.. value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(entry => !entry.Contains("$(", StringComparison.Ordinal))];

    private static IReadOnlyList<ProjectReference> ReferencesOf(XElement? project, string directory) =>
    [
        .. Elements(project, "ProjectReference")
            .Select(element => Reference(element, directory))
            .Where(reference => reference is not null)
            .Select(reference => reference!)
    ];

    private static ProjectReference? Reference(XElement element, string directory)
    {
        var include = element.Attribute("Include")?.Value;

        if (string.IsNullOrWhiteSpace(include) || include.Contains("$(", StringComparison.Ordinal))
        {
            return null;
        }

        return new ProjectReference(
            // .sln and .csproj both write Windows separators regardless of platform, and
            // neither canonicalises. Both halves matter on Linux.
            Path.GetFullPath(Path.Combine(directory, include.Replace('\\', Path.DirectorySeparatorChar))),
            !IsFalse(Metadata(element, "ReferenceOutputAssembly")),
            string.Equals(Metadata(element, "OutputItemType"), "Analyzer", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> PackageIdsOf(XElement? project) =>
    [
        .. Elements(project, "PackageReference")
            .Select(element => element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
    ];

    /// <summary>Reads an attribute or a child element — MSBuild metadata can be written either way.</summary>
    private static string? Metadata(XElement element, string name) =>
        element.Attribute(name)?.Value
        ?? element.Elements().FirstOrDefault(child => child.Name.LocalName == name)?.Value;

    private static bool IsFalse(string? value) =>
        string.Equals(value?.Trim(), "false", StringComparison.OrdinalIgnoreCase);

    /// <summary>The last declaration wins, which is what MSBuild does for an unconditioned property.</summary>
    private static string? Property(XElement? project, string name) =>
        Descendants(project, name).LastOrDefault()?.Value.Trim();

    private static IEnumerable<XElement> Elements(XElement? project, string name) =>
        Descendants(project, name);

    /// <summary>Matched on local name, because a project file may or may not carry the MSBuild namespace.</summary>
    private static IEnumerable<XElement> Descendants(XElement? project, string name) =>
        project?.Descendants().Where(element => element.Name.LocalName == name) ?? [];
}
