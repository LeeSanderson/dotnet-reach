using System.Diagnostics;
using System.Text;

namespace Reach.Processes;

/// <summary>The one implementation of <see cref="IProcessRunner"/>: a real child process.</summary>
internal sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process { StartInfo = StartInfoFor(request) };

        process.Start();

        // A child that reads standard input waits forever otherwise. Nothing Reach runs
        // wants input, so close it immediately rather than leaving the handle open.
        process.StandardInput.Close();

        // Both reads start here, before the wait, and run concurrently. Draining only one
        // stream deadlocks as soon as the other fills its pipe buffer, which `dotnet build`
        // does routinely, and the symptom is a hang rather than a failure.
        var standardOutput = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var standardError = process.StandardError.ReadToEndAsync(CancellationToken.None);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            KillTree(process);

            // The child is gone, so both reads complete; awaiting them releases the pipes
            // instead of leaving two orphaned tasks holding handles for the run's lifetime.
            await Drain(standardOutput, standardError).ConfigureAwait(false);
            throw;
        }

        return new ProcessResult(
            process.ExitCode,
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
    }

    private static ProcessStartInfo StartInfoFor(ProcessRequest request)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.Executable,
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,

            // git writes UTF-8 regardless of the console code page. Without this, a path
            // carrying a non-ASCII character comes back mojibake on a Windows machine whose
            // console is not UTF-8, and then matches no file in the working tree — which is
            // silent under-selection rather than an error.
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // ArgumentList, not Arguments: the runtime does the platform's own quoting, so an
        // argument containing a space or a quote survives without Reach escaping anything.
        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    /// <summary>
    /// Kills the child and everything it started. The direct child is rarely the whole story:
    /// <c>dotnet build</c> leaves MSBuild node processes behind, and they hold the output
    /// directory open.
    /// </summary>
    private static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited between the cancellation and here.
        }
        catch (NotSupportedException)
        {
            // Some platforms refuse the tree walk; the direct child is still worth killing.
            TryKill(process);
        }
        catch (AggregateException)
        {
            // One or more descendants could not be killed. The direct child still can be.
            TryKill(process);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill();
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    private static async Task Drain(params Task<string>[] reads)
    {
        try
        {
            await Task.WhenAll(reads).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // The pipes were closed under the reader when the child was killed. The
            // cancellation is the news; whatever the child had written is not.
        }
    }
}
