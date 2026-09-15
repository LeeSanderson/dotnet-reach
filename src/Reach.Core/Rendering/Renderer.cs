using Reach.Assemblies;
using Reach.Output;
using Reach.Reporting;
using Reach.Selection;

namespace Reach.Rendering;

/// <summary>How a selection reached the runner.</summary>
internal enum Delivery
{
    /// <summary>On the command line.</summary>
    Inline,

    /// <summary>In a response file Reach wrote, passed as an argument.</summary>
    ResponseFile,

    /// <summary>Across several invocations, because the host has no private channel.</summary>
    Chunked,

    /// <summary>Merged into the settings file the caller supplied.</summary>
    RunSettings,

    /// <summary>Nothing was rendered.</summary>
    None,
}

/// <param name="WillRun">
/// What the rendered filter actually matches, which deliberately disagrees with the selection
/// where the dialect renders with <c>~</c>.
/// </param>
internal sealed record RenderedEntry(
    ProjectSelection Selection,
    SelectionMode Mode,
    Delivery Delivery,
    int WillRun,
    IReadOnlyList<IReadOnlyList<string>> Invocations);

internal sealed record RenderedSelection(
    IReadOnlyList<RenderedEntry> Entries,
    IReadOnlyList<Notice> Notices);

/// <summary>
/// Turns the canonical selection into argument vectors a pipeline can execute.
/// </summary>
/// <remarks>
/// Argv arrays, never shell strings: there are already three escaping layers — the filter
/// grammar, MSBuild and XML — and a shell would be a fourth nobody should own.
/// </remarks>
internal sealed class Renderer(
    IReadOnlyList<AssemblyInstance> instances,
    SelectRequest request,
    ReachDirectory directory)
{
    private readonly List<Notice> notices = [];

    internal RenderedSelection Render(SelectionResult selection)
    {
        var entries = new List<RenderedEntry>();

        // The report's own sort order, because the underivable-moniker case puts one
        // project-wide invocation on the first entry and empty arrays on the rest.
        var ordered = selection.Projects
            .OrderBy(project => project.Project.Path, StringComparer.Ordinal)
            .ThenBy(project => project.TargetFramework, StringComparer.Ordinal)
            .ToArray();

        var projectWideEmitted = new HashSet<string>(Paths.Comparer);

        foreach (var project in ordered)
        {
            entries.Add(RenderOne(project, projectWideEmitted));
        }

        Summarise(entries);

        return new RenderedSelection(entries, notices);
    }

    private RenderedEntry RenderOne(ProjectSelection project, HashSet<string> projectWideEmitted)
    {
        // An empty selection emits nothing at all. Not an empty filter string, which runs
        // everything.
        if (project.Mode == SelectionMode.Skip)
        {
            return new RenderedEntry(project, SelectionMode.Skip, Delivery.None, 0, []);
        }

        var siblings = instances
            .Where(instance => instance.Expected.Project == project.Project)
            .ToArray();

        var multiTargeted = siblings.Length > 1;
        var instance = siblings.FirstOrDefault(candidate =>
            candidate.Expected.TargetFramework == project.TargetFramework);

        var selector = multiTargeted ? FrameworkSelector.Derive(instance?.Assembly.Framework) : null;

        // Underivability is a property of the *project*, not of one instance. `dotnet test`
        // runs every target framework, so one sibling that cannot be pinned makes every
        // sibling's filter reach it — pinning the others correctly would not save the run.
        if (multiTargeted
            && (selector is null || siblings.Any(sibling => FrameworkSelector.Derive(sibling.Assembly.Framework) is null)))
        {
            return ProjectWide(project, projectWideEmitted);
        }

        if (project.Mode == SelectionMode.RunAll)
        {
            return new RenderedEntry(
                project,
                SelectionMode.RunAll,
                Delivery.Inline,
                0,
                [Command(project, selector, [])]);
        }

        return Filtered(project, selector);
    }

    private RenderedEntry Filtered(ProjectSelection project, string? selector)
    {
        var dialect = project.Dialect!;
        var names = project.Selected.Select(test => test.Test.FullyQualifiedName).ToArray();

        // Against every test in the project, not only the selected ones — which is the whole
        // point: a `~` filter naming MyTest also matches MyTest2, and the measurement would
        // read the wrong number without it.
        var willRun = FilterDialect.Matches(
                dialect,
                names,
                project.AllTests.Select(test => test.FullyQualifiedName))
            .Count;

        if (request.RunSettings is { Length: > 0 } settings)
        {
            return WithRunSettings(project, selector, names, willRun, settings);
        }

        var overhead = Command(project, selector, ["--filter"]).Sum(argument => argument.Length + 3);
        var chunks = Chunker.Split(dialect, names, overhead);

        if (chunks.Count == 1)
        {
            return new RenderedEntry(
                project,
                SelectionMode.Filtered,
                Delivery.Inline,
                willRun,
                [Command(project, selector, ["--filter", FilterDialect.Expression(dialect, names)])]);
        }

        // Microsoft.Testing.Platform reads a response file Reach writes and passes as an
        // argument: it occupies no channel the consumer configures, cannot merge with consumer
        // state, cannot be silently intersected, and carries no length limit worth budgeting
        // for. Every other host under `dotnet test` has no such channel and gets chunks.
        return FilterDialect.UsesTestingPlatform(dialect)
            ? ViaResponseFile(project, selector, names, willRun)
            : Chunked(project, selector, chunks, willRun);
    }

    private RenderedEntry ViaResponseFile(
        ProjectSelection project,
        string? selector,
        IReadOnlyList<string> names,
        int willRun)
    {
        var path = SideCar(project, "rsp", 0);

        File.WriteAllLines(path, ["--filter", FilterDialect.Expression(project.Dialect!, names)]);

        Note(
            NoticeCodes.SelectionDeliveredOutOfBand,
            NoticeKind.Environment,
            $"{project.Project.Name} ({project.TargetFramework}): {names.Count} test(s) exceed the "
            + $"command-line ceiling of {CommandLineCeiling.Characters} characters, so the filter "
            + $"was written to {path} and passed as a response file.");

        return new RenderedEntry(
            project,
            SelectionMode.Filtered,
            Delivery.ResponseFile,
            willRun,
            [Command(project, selector, ["@" + path])]);
    }

    private RenderedEntry Chunked(
        ProjectSelection project,
        string? selector,
        IReadOnlyList<IReadOnlyList<string>> chunks,
        int willRun)
    {
        Note(
            NoticeCodes.SelectionDeliveredOutOfBand,
            NoticeKind.Environment,
            $"{project.Project.Name} ({project.TargetFramework}): the selection exceeds the "
            + $"command-line ceiling of {CommandLineCeiling.Characters} characters and this host "
            + $"has no response-file channel, so it runs as {chunks.Count} invocations.");

        return new RenderedEntry(
            project,
            SelectionMode.Filtered,
            Delivery.Chunked,
            willRun,
            [
                .. chunks.Select(chunk =>
                    Command(project, selector, ["--filter", FilterDialect.Expression(project.Dialect!, chunk)]))
            ]);
    }

    private RenderedEntry WithRunSettings(
        ProjectSelection project,
        string? selector,
        IReadOnlyList<string> names,
        int willRun,
        string settings)
    {
        if (RunSettings.CarriesAFilter(settings))
        {
            // A pre-existing TestCaseFilter is AND-ed with Reach's, and a consumer's filter is
            // typically an exclusion — so the intersection would be strictly smaller than the
            // selection, undetectably.
            Note(
                NoticeCodes.RunSettingsFilterConflict,
                NoticeKind.Widening,
                $"{project.Project.Name} ({project.TargetFramework}): {settings} already carries a "
                + "<TestCaseFilter>, which would be AND-ed with Reach's and could only shrink the "
                + "selection. The whole project runs instead.");

            return new RenderedEntry(
                project,
                SelectionMode.RunAll,
                Delivery.Inline,
                0,
                [Command(project, selector, [])]);
        }

        var merged = RunSettings.Merge(
            settings,
            FilterDialect.Expression(project.Dialect!, names),
            SideCar(project, "runsettings", 0));

        return new RenderedEntry(
            project,
            SelectionMode.Filtered,
            Delivery.RunSettings,
            willRun,
            [Command(project, selector, ["--settings", merged])]);
    }

    /// <summary>
    /// The underivable-moniker case: one project-wide invocation with no selector, carried by
    /// the first entry in sort order, and empty arrays on the rest — so a consumer's loop issues
    /// exactly one command. <c>mode</c> is the field that says there is something to run; an
    /// empty array alone does not mean "nothing".
    /// </summary>
    private RenderedEntry ProjectWide(ProjectSelection project, HashSet<string> emitted)
    {
        if (!emitted.Add(project.Project.Path))
        {
            return new RenderedEntry(project, SelectionMode.RunAll, Delivery.None, 0, []);
        }

        Note(
            NoticeCodes.FrameworkSelectorUnderivable,
            NoticeKind.Widening,
            $"{project.Project.Name} targets more than one framework and at least one of them "
            + "carries a platform suffix, whose declared moniker cannot be recovered from "
            + "metadata. The whole project runs, on every target framework.");

        return new RenderedEntry(project, SelectionMode.RunAll, Delivery.Inline, 0, [Command(project, null, [])]);
    }

    /// <summary>
    /// The argv. <strong>Never <c>-o</c></strong>: MTP-mode <c>dotnet test</c> has no <c>-o</c>
    /// at all and spells <c>--output</c> to mean test output <em>verbosity</em>, while
    /// VSTest-mode <c>dotnet test</c> and <c>dotnet build</c> use it for a directory.
    /// </summary>
    private IReadOnlyList<string> Command(ProjectSelection project, string? selector, IReadOnlyList<string> filter)
    {
        var arguments = new List<string> { "dotnet", "test", project.Project.Path };

        if (selector is not null)
        {
            arguments.Add("-f");
            arguments.Add(selector);
        }

        if (request.Configuration is { Length: > 0 } configuration)
        {
            // The same reasoning that passes it to the build: a selection over Release output
            // that the runner then executes against Debug is a different set of tests.
            arguments.Add("--configuration");
            arguments.Add(configuration);
        }

        arguments.AddRange(filter);

        return arguments;
    }

    /// <summary>
    /// In the directory Reach owns, named deterministically from project, framework and chunk,
    /// so a re-run overwrites rather than accumulates. Never a path MSBuild or Visual Studio
    /// reads by convention, and never the system temp directory, which agents clear between
    /// steps and which is the least inspectable place to look when a filter misbehaves.
    /// </summary>
    private string SideCar(ProjectSelection project, string extension, int index)
    {
        directory.EnsureCreated();

        return directory.Combine(
            $"{project.Project.Name}.{project.TargetFramework}.{index}.{extension}");
    }

    private void Summarise(IReadOnlyList<RenderedEntry> entries)
    {
        var overMatching = entries
            .Where(entry => entry.Mode == SelectionMode.Filtered)
            .Where(entry => entry.WillRun > entry.Selection.Selected.Count)
            .ToArray();

        if (overMatching.Length > 0)
        {
            Note(
                NoticeCodes.DialectOverSelects,
                NoticeKind.Widening,
                "The rendered filter matches more tests than were selected, because the dialect "
                + "matches by containment rather than equality: "
                + string.Join(
                    ", ",
                    overMatching.Select(entry =>
                        $"{entry.Selection.Project.Name} ({entry.Selection.TargetFramework}) "
                        + $"{entry.Selection.Selected.Count} selected, {entry.WillRun} will run")));
        }
    }

    private void Note(string code, NoticeKind kind, string message) =>
        notices.Add(new Notice(code, kind, message));
}
