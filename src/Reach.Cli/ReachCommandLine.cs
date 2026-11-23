using System.CommandLine;
using System.Globalization;
using Reach.Output;
using Reach.Processes;
using Reach.Reporting;

namespace Reach.Cli;

/// <summary>
/// The command tree. One verb, one positional, eleven options.
/// </summary>
/// <remarks>
/// <para>
/// Option spellings mirror <c>dotnet build</c> exactly — same long names, same short forms,
/// same semantics — so a pipeline author transfers what they already know and Reach never
/// invents a synonym for a concept the SDK has named. <c>--artifacts-path</c> is hyphenated
/// and has no short form on any <c>dotnet</c> command, so neither does Reach's. That rule
/// governs <em>spelling</em>, not resolution semantics: where a resolution rule could narrow
/// scope, the correctness rule outranks the convention.
/// </para>
/// </remarks>
internal sealed class ReachCommandLine
{
    private readonly Argument<string?> _target = new("SOLUTION|PROJECT")
    {
        Arity = ArgumentArity.ZeroOrOne,
        Description = "The solution or single test project. Discovered in the working directory when omitted.",
    };

    private readonly Option<string?> _baseOption = new("--base")
    {
        Description = "The branch this work merges into. The baseline is merge-base(HEAD, <ref>). Auto-detected when omitted.",
        HelpName = "ref",
    };

    private readonly Option<string?> _configuration = new("--configuration", "-c")
    {
        Description = "Build configuration. The SDK's default when omitted.",
        HelpName = "name",
    };

    private readonly Option<string?> _output = new("--output", "-o")
    {
        Description = "The build's output directory. Never where Reach writes — see --report-dir.",
        HelpName = "dir",
    };

    private readonly Option<string?> _artifactsPath = new("--artifacts-path")
    {
        Description = "The build's artifacts path.",
        HelpName = "dir",
    };

    private readonly Option<bool> _noBuild = new("--no-build")
    {
        Description = "Analyse the output already on disk instead of building first.",
    };

    private readonly Option<string?> _report = new("--report")
    {
        Description = "Where to write report.json. '-' sends it to standard output and moves the summary to standard error.",
        HelpName = "path|-",
    };

    private readonly Option<string?> _reportDirectory = new("--report-dir")
    {
        Description = $"The directory Reach owns, holding the report and every response file and testlist. Defaults to {ReachDirectory.DefaultName} under the working directory.",
        HelpName = "dir",
    };

    private readonly Option<bool> _paths = new("--paths")
    {
        Description = "Add hop-by-hop paths to each selected test in the report, from the test back to the change. Every hop is a method.",
    };

    private readonly Option<string?> _runSettings = new("--runsettings")
    {
        // Documented by its effect. The name is a foot-gun: someone will pass it expecting
        // Reach to *use* their settings for a test run Reach never performs.
        Description = "Merge Reach's filter into this runsettings file and write the result. Reach runs no tests.",
        HelpName = "path",
    };

    private readonly Option<string?> _verbosity = new("--verbosity", "-v")
    {
        Description = "Set the verbosity level. Allowed values are q[uiet], m[inimal], n[ormal], d[etailed] and diag[nostic].",
        HelpName = "level",
    };

    private readonly Option<bool> _noColor = new("--no-color")
    {
        Description = "Do not colour the summary. Auto-detected, and NO_COLOR is honoured too.",
    };

    private readonly RootCommand _root;

    internal ReachCommandLine(Func<SelectRequest, CancellationToken, Task<int>> select)
    {
        _verbosity.AcceptOnlyFromAmong([.. Verbosities.Spellings]);

        var selectCommand = new Command(
            "select",
            "Work out which tests a change could possibly affect, and write a report naming them.")
        {
            _target,
            _baseOption,
            _configuration,
            _output,
            _artifactsPath,
            _noBuild,
            _report,
            _reportDirectory,
            _paths,
            _runSettings,
            _verbosity,
            _noColor,
        };

        selectCommand.SetAction((parseResult, cancellationToken) =>
            select(RequestFrom(parseResult), cancellationToken));

        _root = new RootCommand(
            "Reach works out which tests a code change could possibly affect, by reading "
            + "compiled assemblies and walking the call graph backwards from changed code to tests.")
        {
            selectCommand,
        };
    }

    /// <summary>
    /// The forwarded build arguments are supplied separately because Reach splits the argument
    /// vector at <c>--</c> itself, before the parser sees it.
    /// </summary>
    internal ParseResult Parse(IReadOnlyList<string> arguments) => _root.Parse(arguments);

    internal SelectRequest RequestFrom(ParseResult parseResult) => new()
    {
        Target = parseResult.GetValue(_target),
        Base = parseResult.GetValue(_baseOption),
        Configuration = parseResult.GetValue(_configuration),
        Output = parseResult.GetValue(_output),
        ArtifactsPath = parseResult.GetValue(_artifactsPath),
        NoBuild = parseResult.GetValue(_noBuild),
        Report = parseResult.GetValue(_report),
        ReportDirectory = parseResult.GetValue(_reportDirectory),
        Paths = parseResult.GetValue(_paths),
        RunSettings = parseResult.GetValue(_runSettings),
        Verbosity = Verbosities.Parse(parseResult.GetValue(_verbosity)),
        NoColor = parseResult.GetValue(_noColor),
    };
}

/// <summary>
/// Everything the entry point does: split the argument vector, parse, run, print.
/// </summary>
internal static class ReachCli
{
    internal static async Task<int> RunAsync(
        IReadOnlyList<string> arguments,
        string workingDirectory,
        TextWriter standardOutput,
        TextWriter standardError,
        EnvironmentLookup environment,
        bool outputIsRedirected,
        CancellationToken cancellationToken = default)
    {
        // Before the parser, because System.CommandLine's positional argument would otherwise
        // swallow the first forwarded token when no target was given.
        var (reachArguments, buildArguments) = ForwardedBuildArguments.Split(arguments);

        var pipeline = new ReachPipeline(new ProcessRunner());

        var commandLine = new ReachCommandLine(async (parsed, token) =>
        {
            var request = parsed with
            {
                WorkingDirectory = workingDirectory,
                ForwardedBuildArguments = buildArguments,
            };

            var timings = new PhaseTimings();

            var run = await pipeline
                .SelectAsync(request, environment, timings, token)
                .ConfigureAwait(false);

            Print(run, request, timings, standardOutput, standardError, environment, outputIsRedirected);

            return Convert.ToInt32(run.ExitCode, CultureInfo.InvariantCulture);
        });

        return await commandLine
            .Parse(reachArguments)
            .InvokeAsync(
                new InvocationConfiguration { Output = standardOutput, Error = standardError },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static void Print(
        SelectRun run,
        SelectRequest request,
        PhaseTimings timings,
        TextWriter standardOutput,
        TextWriter standardError,
        EnvironmentLookup environment,
        bool outputIsRedirected)
    {
        var directory = ReachDirectory.For(request.WorkingDirectory, request.ReportDirectory);
        var destination = ReportDestination.Resolve(request.Report, directory, request.WorkingDirectory);

        var streams = Streams.For(
            destination,
            standardOutput,
            standardError,
            ColourSupport.Enabled(request.NoColor, environment, outputIsRedirected));

        if (run.Message.Length > 0)
        {
            streams.Notices.WriteLine(run.Message);
        }

        if (!run.WritesReport)
        {
            return;
        }

        var report = ReportBuilder.Build(run, request, timings);
        var writtenTo = ReportWriter.Write(report, destination, streams);

        streams.Summary.Write(HumanSummary.Write(report, writtenTo, directory.Path));
    }
}
