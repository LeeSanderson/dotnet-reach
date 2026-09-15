using Reach.Tests.Fixtures;

namespace Reach.Tests.Assemblies;

/// <summary>
/// The asymmetry that inverts what people expect: <strong>the mode that trusts the caller more
/// is the mode that detects more.</strong> Under <c>--no-build</c> an edited source file fails
/// the check; in default mode the build recompiles it first, every checksum agrees, and the
/// run proceeds.
/// </summary>
/// <remarks>
/// Driven through the whole pipeline, with the build scripted at the one port. What is being
/// asserted is the ordering — build, then verify — and the scripted build does to the output
/// directory exactly what a real one would.
/// </remarks>
public class CorrespondenceInBothModesTests
{
    private const string Before = "namespace N; public class Widget { public int Spin() => 1; }";
    private const string After = "namespace N; public class Widget { public int Spin() => 42; }";

    private readonly ScriptedProcessRunner runner = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_edited_file_under_no_build_is_exit_5_naming_the_document()
    {
        using var repository = Repository(out var sourcePath);

        Compile(repository, sourcePath, Before);

        // The binaries belong to a different revision of this file, and no timestamp would say
        // so: git does not preserve them, so checking out an older commit onto a warm agent
        // can leave source *older* than the binaries beside it.
        File.WriteAllText(sourcePath, After);

        var run = await Select(repository, noBuild: true);

        Assert.Equal(ExitCode.CorrespondenceFailed, run.ExitCode);
        Assert.Contains("Widget.cs", run.Message);
    }

    [Fact]
    public async Task The_same_edit_in_default_mode_proceeds_because_the_build_recompiled_it()
    {
        using var repository = Repository(out var sourcePath);

        Compile(repository, sourcePath, Before);
        File.WriteAllText(sourcePath, After);

        // What a real `dotnet build` would do with an edited file.
        runner.Performs(() => Compile(repository, sourcePath, After), "build");

        var run = await Select(repository, noBuild: false);

        Assert.NotEqual(ExitCode.CorrespondenceFailed, run.ExitCode);
        Assert.Contains(runner.Requests, request => request.Arguments.Contains("build"));
    }

    [Fact]
    public async Task No_build_never_invokes_the_build()
    {
        using var repository = Repository(out var sourcePath);

        Compile(repository, sourcePath, Before);

        await Select(repository, noBuild: true);

        Assert.DoesNotContain(runner.Requests, request => request.Executable == "dotnet");
    }

    private async Task<SelectRun> Select(TempRepository repository, bool noBuild)
    {
        // git is real; only the build is scripted.
        var real = new Reach.Processes.ProcessRunner();

        return await new ReachPipeline(new SplitRunner(runner, real))
            .SelectAsync(
                new SelectRequest
                {
                    WorkingDirectory = repository.Path,
                    Base = "main",
                    NoBuild = noBuild,
                },
                _ => null,
                new Reach.Reporting.PhaseTimings(),
                Token);
    }

    private static void Compile(TempRepository repository, string sourcePath, string source)
    {
        File.WriteAllText(sourcePath, source);

        Compiled
            .Assembly("Core", source, sourcePath)
            .WriteTo(repository.Combine("src", "Core", "bin", "Debug", "net10.0"));

        Compiled
            .Assembly("Tests", "namespace N.Tests; public class T { public void M() { } }",
                repository.Combine("src", "Tests", "Tests.cs"))
            .WriteTo(repository.Combine("src", "Tests", "bin", "Debug", "net10.0"));
    }

    private static TempRepository Repository(out string sourcePath)
    {
        var repository = TempRepository.Create();

        repository.WriteFile(".gitignore", "bin/\nobj/\n");

        repository.WriteFile(
            "src/Core/Core.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

        repository.WriteFile(
            "src/Tests/Tests.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="xunit.v3" Version="4.0.0" />
                <ProjectReference Include="../Core/Core.csproj" />
              </ItemGroup>
            </Project>
            """);

        repository.WriteFile(
            "Solution.slnx",
            """
            <Solution>
              <Project Path="src/Core/Core.csproj" />
              <Project Path="src/Tests/Tests.csproj" />
            </Solution>
            """);

        sourcePath = repository.Combine("src", "Core", "Widget.cs");
        File.WriteAllText(sourcePath, Before);
        repository.WriteFile("src/Tests/Tests.cs", "namespace N.Tests; public class T { public void M() { } }");

        repository.Commit("baseline");

        return repository;
    }
}

/// <summary>
/// Sends <c>dotnet</c> to the fake and everything else — which is only ever <c>git</c> — to a
/// real process, so a test can script the build without also scripting seven git commands.
/// </summary>
internal sealed class SplitRunner(Reach.Processes.IProcessRunner scripted, Reach.Processes.IProcessRunner real)
    : Reach.Processes.IProcessRunner
{
    public Task<Reach.Processes.ProcessResult> RunAsync(
        Reach.Processes.ProcessRequest request,
        CancellationToken cancellationToken = default) =>
        request.Executable == "dotnet"
            ? scripted.RunAsync(request, cancellationToken)
            : real.RunAsync(request, cancellationToken);
}
