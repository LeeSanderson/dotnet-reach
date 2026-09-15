namespace Reach.Processes;

/// <summary>
/// The only port in Reach. It covers two adapters — <c>git</c> and <c>dotnet build</c> —
/// which is what makes it a real seam rather than a hypothetical one.
/// </summary>
/// <remarks>
/// Deliberately much smaller than what it hides: process lifetime, concurrent draining of
/// both output streams, killing the child's whole process tree on cancellation, and the
/// decision that a non-zero exit is a value rather than a throw. Its fake is what makes a
/// shallow clone, five CI environment variables and a failed build testable without any of
/// those things being true.
/// </remarks>
internal interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default);
}
