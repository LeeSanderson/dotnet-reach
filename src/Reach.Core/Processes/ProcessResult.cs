namespace Reach.Processes;

/// <summary>
/// What a child process did. A non-zero exit code is a result, never an exception: every
/// caller in Reach has something to say about a failure, and none of them want a stack trace.
/// </summary>
internal sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    internal bool Succeeded => ExitCode == 0;

    /// <summary>Standard output with the trailing newline removed — what a single-value command returns.</summary>
    internal string Value => StandardOutput.TrimEnd('\r', '\n');

    /// <summary>Standard output split into lines, with the trailing empty line removed.</summary>
    internal IReadOnlyList<string> Lines =>
        StandardOutput.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .ToArray();
}
