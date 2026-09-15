using Reach.Changes;
using Reach.Reporting;

namespace Reach.Tests.Fixtures;

/// <summary>Builds a report from a <see cref="Selections"/> fixture, or from a real run.</summary>
internal static class ReportFixture
{
    internal static Report Build(Selections selections, bool noChanges = false, SelectRequest? request = null)
    {
        using var directory = TempDirectory.Create("reach-render");

        var actual = (request ?? new SelectRequest()) with { WorkingDirectory = directory.Path };

        // With no changes there are no roots, so the selector selects nothing and every entry
        // is a skip — which is what the pipeline itself produces, and what the report's
        // "a consumer's loop behaves identically across all four outcomes" rests on.
        var selection = noChanges ? Emptied(selections.Result) : selections.Result;

        var rendered = new Reach.Rendering.Renderer(
                selections.Instances,
                actual,
                Reach.Output.ReachDirectory.For(directory.Path, null))
            .Render(selection);

        var run = new SelectRun(
            ExitCode.Success,
            string.Empty,
            [.. selections.Result.Notices, .. rendered.Notices],
            Scope: selections.Scope,
            Changes: noChanges ? ChangedSet.Empty : selections.Changed,
            Assemblies: selections.Instances,
            Selection: selection,
            Rendered: rendered);

        return ReportBuilder.Build(run, actual, new PhaseTimings());
    }

    private static Reach.Selection.SelectionResult Emptied(Reach.Selection.SelectionResult selection) =>
        selection with
        {
            Projects =
            [
                .. selection.Projects.Select(project => project with
                {
                    Mode = Reach.Selection.SelectionMode.Skip,
                    Selected = [],
                })
            ],
            Changes = [],
        };

    /// <summary>The smallest report that serialises, for assertions about the shape itself.</summary>
    internal static Report Minimal(ReportCounts? counts = null) =>
        new()
        {
            Outcome = "selected",
            Envelope = new ReportEnvelope
            {
                ToolVersion = "0.0.0",
                ChangeSources = [],
                BuildMode = "no-build",
                ForwardedBuildArguments = [],
                Correspondence = "skipped",
                Timings = new ReportTimings
                {
                    StartedUtc = "2026-01-01T00:00:00.0000000+00:00",
                    TotalMs = 0,
                    Phases = new SortedDictionary<string, long>(),
                },
            },
            Scope = new ReportScope { TestProjects = [], Assemblies = [] },
            Changes = [],
            Entries = counts is null
                ? []
                : [
                    new ReportEntry
                    {
                        Project = "tests/T/T.csproj",
                        TargetFramework = "net10.0",
                        Mode = "run-all",
                        Counts = counts,
                        Tests = [],
                        Invocations = [],
                    },
                ],
            Notices = [],
        };

    /// <summary>A real repository with real assemblies, for the determinism assertion.</summary>
    internal static TempRepository Repository()
    {
        var repository = TempRepository.Create("reach-report");

        repository.WriteFile(".gitignore", "bin/\nobj/\n");

        repository.WriteFile(
            "src/Core/Core.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");

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

        var corePath = repository.Combine("src", "Core", "Widget.cs");
        var testsPath = repository.Combine("tests", "Tests", "WidgetTests.cs");

        const string CoreSource = "namespace N; public class Widget { public int Spin() => 1; }";
        const string TestsSource =
            "namespace N.Tests; public class WidgetTests { [Xunit.Fact] public void Spins() => new N.Widget().Spin(); }";

        File.WriteAllText(corePath, CoreSource);
        File.WriteAllText(testsPath, TestsSource);

        repository.Commit("baseline");

        var core = Compiled.Assembly("Core", CoreSource, corePath);
        core.WriteTo(repository.Combine("src", "Core", "bin", "Debug", "net10.0"));

        Compiled
            .Assembly("Tests", TestsSource, testsPath, references: [core])
            .WriteTo(repository.Combine("tests", "Tests", "bin", "Debug", "net10.0"));

        // A change after the baseline, so the run has something to analyse.
        File.WriteAllText(corePath, "namespace N; public class Widget { public int Spin() => 2; }");

        var changed = Compiled.Assembly(
            "Core",
            "namespace N; public class Widget { public int Spin() => 2; }",
            corePath);

        changed.WriteTo(repository.Combine("src", "Core", "bin", "Debug", "net10.0"));

        return repository;
    }

    /// <summary>Runs the pipeline over <paramref name="repository"/> and returns the report's JSON.</summary>
    internal static async Task<string> RunAsync(TempRepository repository, CancellationToken cancellationToken)
    {
        var run = await new ReachPipeline(new Reach.Processes.ProcessRunner())
            .SelectAsync(
                new SelectRequest
                {
                    WorkingDirectory = repository.Path,
                    Base = "main",
                    NoBuild = true,
                },
                _ => null,
                new PhaseTimings(),
                cancellationToken);

        Assert.Equal(ExitCode.Success, run.ExitCode);

        return ReportWriter.Serialise(
            ReportBuilder.Build(
                run,
                new SelectRequest { WorkingDirectory = repository.Path, NoBuild = true },
                new PhaseTimings()));
    }
}
