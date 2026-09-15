using Reach.Processes;

namespace Reach.Tests.Fixtures;

/// <summary>
/// An <see cref="IProcessRunner"/> that answers from a script instead of starting anything.
/// It is what makes a shallow clone, a missing merge-base and a failed build testable
/// without any of those things being true.
/// </summary>
/// <remarks>
/// Matching is deliberately loose and recording is deliberately exact. A script matches when
/// its arguments appear in the request's vector in order, so a test says what it cares about
/// — <c>merge-base HEAD</c> — without restating the global options every git call carries.
/// Assertions then read <see cref="Requests"/>, which holds the vector exactly as issued.
/// </remarks>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    private readonly List<(string[] Arguments, ProcessResult Result)> scripts = [];
    private readonly List<ProcessRequest> requests = [];

    /// <summary>Every request made, in order, exactly as issued.</summary>
    internal IReadOnlyList<ProcessRequest> Requests => requests;

    internal ScriptedProcessRunner Succeeds(string standardOutput, params string[] arguments) =>
        Returns(new ProcessResult(0, standardOutput, string.Empty), arguments);

    internal ScriptedProcessRunner Fails(int exitCode, string standardError, params string[] arguments) =>
        Returns(new ProcessResult(exitCode, string.Empty, standardError), arguments);

    internal ScriptedProcessRunner Returns(ProcessResult result, params string[] arguments)
    {
        scripts.Add((arguments, result));
        return this;
    }

    public Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        requests.Add(request);

        foreach (var (arguments, result) in scripts)
        {
            if (Matches(request.Arguments, arguments))
            {
                return Task.FromResult(result);
            }
        }

        // Louder than returning a default: a fake that silently answers an unexpected
        // command turns the test above it into decoration.
        throw new InvalidOperationException(
            $"No script matches: {request}. Scripted: "
            + string.Join("; ", scripts.Select(script => string.Join(' ', script.Arguments))));
    }

    private static bool Matches(IReadOnlyList<string> issued, IReadOnlyList<string> wanted)
    {
        var next = 0;

        foreach (var argument in issued)
        {
            if (next < wanted.Count && argument == wanted[next])
            {
                next++;
            }
        }

        return next == wanted.Count;
    }
}
