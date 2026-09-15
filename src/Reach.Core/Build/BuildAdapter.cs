using Reach.Processes;
using Reach.Projects;

namespace Reach.Build;

/// <summary>What the build did, and the exact command that did it.</summary>
/// <param name="Request">
/// Null when <c>--no-build</c> meant no build was run. Recorded otherwise so a surprising run
/// stays reproducible from the report envelope alone.
/// </param>
internal sealed record BuildOutcome(bool Succeeded, string Output, ProcessRequest? Request)
{
    internal static BuildOutcome Skipped { get; } = new(true, string.Empty, null);
}

/// <summary>
/// The second adapter at the one port.
/// </summary>
/// <remarks>
/// On an already-built tree this is a fast no-op, because MSBuild's own up-to-date checking
/// decides. <strong>Reach implements no up-to-date check of its own</strong> — a second
/// opinion about whether a build is needed is a second thing to be wrong about.
/// </remarks>
internal sealed class BuildAdapter(IProcessRunner runner)
{
    internal async Task<BuildOutcome> BuildAsync(
        DiscoveredTarget target,
        SelectRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.NoBuild)
        {
            return BuildOutcome.Skipped;
        }

        var processRequest = new ProcessRequest("dotnet", Arguments(target, request), request.WorkingDirectory);
        var result = await runner.RunAsync(processRequest, cancellationToken).ConfigureAwait(false);

        return new BuildOutcome(
            result.Succeeded,
            result.StandardOutput + result.StandardError,
            processRequest);
    }

    private static IReadOnlyList<string> Arguments(DiscoveredTarget target, SelectRequest request)
    {
        var arguments = new List<string> { "build", target.Path };

        // The layout options are filters on the scan, but they are also the build's own
        // switches. Passing them is what makes Reach's answer and the build's output agree —
        // and is why a forwarded copy of one after `--` is refused.
        Add(arguments, "--configuration", request.Configuration);
        Add(arguments, "--output", request.Output);
        Add(arguments, "--artifacts-path", request.ArtifactsPath);

        // Everything after `--`, verbatim. Without this, every adopter who builds with `-p:`
        // properties, `--no-restore` or a binlog is pushed onto `--no-build` permanently.
        arguments.AddRange(request.ForwardedBuildArguments);

        return arguments;
    }

    private static void Add(List<string> arguments, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            arguments.Add(name);
            arguments.Add(value);
        }
    }
}
