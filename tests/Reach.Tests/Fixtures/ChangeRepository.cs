using Reach.Changes;
using Reach.Git;
using Reach.Processes;
using Reach.Projects;

namespace Reach.Tests.Fixtures;

/// <summary>
/// A temporary git repository holding a two-project solution, ready to have a change applied
/// to it and the changed set read back.
/// </summary>
/// <remarks>
/// A fixture committed in Reach's own repository has Reach's history, which is not a usable
/// baseline — so change detection is exercised against a repository built per test.
/// </remarks>
internal sealed class ChangeRepository : IDisposable
{
    private readonly TempRepository repository;

    private ChangeRepository(TempRepository repository) => this.repository = repository;

    internal string Path => repository.Path;

    internal static ChangeRepository Create()
    {
        var repository = TempRepository.Create("reach-changes");

        // Every .NET repository gitignores obj/ and bin/, and --exclude-standard is what keeps
        // the generated .cs files under them out of the changed set.
        repository.WriteFile(".gitignore", "obj/\nbin/\n");

        repository.WriteFile(
            "src/Core/Core.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        repository.WriteFile(
            "tests/Tests/Tests.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit.v3" Version="4.0.0" />
                <ProjectReference Include="../../src/Core/Core.csproj" />
              </ItemGroup>
            </Project>
            """);

        repository.WriteFile(
            "Solution.slnx",
            """
            <Solution>
              <Project Path="src/Core/Core.csproj" />
              <Project Path="tests/Tests/Tests.csproj" />
            </Solution>
            """);

        return new ChangeRepository(repository);
    }

    internal void Write(string relativePath, string source) =>
        repository.WriteFile(relativePath, source);

    internal void Delete(string relativePath) =>
        File.Delete(System.IO.Path.Combine(Path, relativePath));

    internal void Move(string from, string to)
    {
        var target = System.IO.Path.Combine(Path, to);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(target)!);
        File.Move(System.IO.Path.Combine(Path, from), target);
    }

    internal string Commit(string message = "change") => repository.Commit(message);

    internal void Git(params string[] arguments) => repository.Git(arguments);

    /// <summary>Commits everything so far and returns the SHA to measure changes against.</summary>
    internal string CommitBaseline() => repository.Commit("baseline");

    internal async Task<ChangedSet> ChangesAsync(string baseline, CancellationToken cancellationToken)
    {
        var discovery = TargetDiscovery.Discover(System.IO.Path.Combine(Path, "Solution.slnx"));
        Assert.True(discovery.Discovered, discovery.Message);

        var scope = await AnalysisScopeResolver.ResolveAsync(discovery.Target!, cancellationToken);
        Assert.True(scope.Resolved, scope.Message);

        var git = new GitAdapter(new ProcessRunner(), Path);

        return await new ChangedSetBuilder(git, Path, scope.Scope!)
            .BuildAsync(baseline, cancellationToken);
    }

    public void Dispose() => repository.Dispose();
}
