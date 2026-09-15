namespace Reach.Tests.Fixtures;

/// <summary>
/// Real project and solution files in a temporary directory. Nothing here is built — every
/// assertion in ticket 04 is about what the files say, and MSBuild is never invoked.
/// </summary>
internal sealed class ProjectTree : IDisposable
{
    private readonly TempDirectory directory;

    private ProjectTree(TempDirectory directory) => this.directory = directory;

    internal string Root => directory.Path;

    internal static ProjectTree Create() => new(TempDirectory.Create("reach-tree"));

    /// <summary>Writes <c>src/<paramref name="name"/>/<paramref name="name"/>.csproj</c>.</summary>
    internal string AddProject(
        string name,
        IEnumerable<string>? references = null,
        IEnumerable<string>? packages = null,
        string? targetFrameworks = null,
        string? assemblyName = null,
        IEnumerable<(string Reference, string Attributes)>? decoratedReferences = null)
    {
        var relative = Path.Combine("src", name, name + ".csproj");
        var projectDirectory = Path.GetDirectoryName(directory.Combine(relative))!;

        var properties = new List<string>();

        if (targetFrameworks is not null)
        {
            properties.Add(targetFrameworks.Contains(';', StringComparison.Ordinal)
                ? $"<TargetFrameworks>{targetFrameworks}</TargetFrameworks>"
                : $"<TargetFramework>{targetFrameworks}</TargetFramework>");
        }

        if (assemblyName is not null)
        {
            properties.Add($"<AssemblyName>{assemblyName}</AssemblyName>");
        }

        var items = new List<string>();

        foreach (var reference in references ?? [])
        {
            items.Add($"""<ProjectReference Include="{Relative(projectDirectory, reference)}" />""");
        }

        foreach (var (reference, attributes) in decoratedReferences ?? [])
        {
            items.Add(
                $"""<ProjectReference Include="{Relative(projectDirectory, reference)}" {attributes} />""");
        }

        foreach (var package in packages ?? [])
        {
            items.Add($"""<PackageReference Include="{package}" Version="1.0.0" />""");
        }

        directory.WriteFile(
            relative,
            $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {string.Join("\n    ", properties)}
              </PropertyGroup>
              <ItemGroup>
                {string.Join("\n    ", items)}
              </ItemGroup>
            </Project>
            """);

        return directory.Combine(relative);
    }

    internal string WriteFile(string relativePath, string contents) =>
        directory.WriteFile(relativePath, contents);

    internal string Combine(params string[] parts) => directory.Combine(parts);

    /// <summary>Writes a <c>.slnx</c>, the default for <c>dotnet new sln</c> on .NET 10.</summary>
    internal string AddSlnx(string name, params string[] projects)
    {
        var entries = projects.Select(project =>
            $"""  <Project Path="{Relative(Root, project)}" />""");

        return directory.WriteFile(
            name + ".slnx",
            $"""
            <Solution>
            {string.Join("\n", entries)}
            </Solution>
            """);
    }

    /// <summary>
    /// Writes a <c>.sln</c> in the V12 format, with Windows separators, because that is what a
    /// solution written on Windows carries wherever it is later read.
    /// </summary>
    internal string AddSln(string name, params string[] projects)
    {
        var body = new System.Text.StringBuilder();

        body.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");
        body.AppendLine("# Visual Studio Version 17");

        var ids = new List<string>();

        // The C# project type GUID. .sln and .slnx use different GUIDs for the same project,
        // which is why production code switches on Extension rather than on Type.
        const string CSharpProjectType = "{9A19103F-16F7-4668-BE54-9A1E7A4F7556}";

        foreach (var project in projects)
        {
            var id = Guid.NewGuid().ToString("B").ToUpperInvariant();
            var path = Relative(Root, project).Replace('/', '\\');
            ids.Add(id);

            body.AppendLine(
                $"""Project("{CSharpProjectType}") = "{Path.GetFileNameWithoutExtension(project)}", "{path}", "{id}" """.TrimEnd());
            body.AppendLine("EndProject");
        }

        body.AppendLine("Global");
        body.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
        body.AppendLine("\t\tDebug|Any CPU = Debug|Any CPU");
        body.AppendLine("\t\tRelease|Any CPU = Release|Any CPU");
        body.AppendLine("\tEndGlobalSection");
        body.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

        foreach (var id in ids)
        {
            body.AppendLine($"\t\t{id}.Debug|Any CPU.ActiveCfg = Debug|Any CPU");
            body.AppendLine($"\t\t{id}.Debug|Any CPU.Build.0 = Debug|Any CPU");
            body.AppendLine($"\t\t{id}.Release|Any CPU.ActiveCfg = Release|Any CPU");
            body.AppendLine($"\t\t{id}.Release|Any CPU.Build.0 = Release|Any CPU");
        }

        body.AppendLine("\tEndGlobalSection");
        body.AppendLine("EndGlobal");

        return directory.WriteFile(name + ".sln", body.ToString());
    }

    /// <summary>Writes a solution filter naming <paramref name="solution"/>.</summary>
    internal string AddSlnf(string name, string solution, params string[] projects)
    {
        var included = string.Join(
            ",\n        ",
            projects.Select(project => $"\"{JsonPath(project)}\""));

        return directory.WriteFile(
            name + ".slnf",
            $$"""
            {
              "solution": {
                "path": "{{JsonPath(solution)}}",
                "projects": [
                    {{included}}
                ]
              }
            }
            """);
    }

    /// <summary>
    /// Windows separators, doubled: a real .slnf carries backslashes, and JSON requires them
    /// escaped. A fixture writing single backslashes produces a file no parser accepts.
    /// </summary>
    private string JsonPath(string path) => Relative(Root, path).Replace("/", "\\\\", StringComparison.Ordinal);

    private static string Relative(string from, string to) =>
        Path.GetRelativePath(from, to).Replace('\\', '/');

    public void Dispose() => directory.Dispose();
}
