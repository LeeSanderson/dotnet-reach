namespace Reach.Processes;

/// <summary>
/// One command to run: an argument vector, never a shell string. Reach never builds a
/// command line by concatenation, so a path containing a space or a quote is never a
/// parsing problem.
/// </summary>
/// <param name="Executable">The program to run, resolved through <c>PATH</c> if not rooted.</param>
/// <param name="Arguments">The argument vector, one element per argument, already unquoted.</param>
/// <param name="WorkingDirectory">The directory the child starts in.</param>
internal sealed record ProcessRequest(
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory)
{
    /// <summary>The command as a human would type it. For notices and report entries only.</summary>
    public override string ToString() =>
        string.Join(' ', new[] { Executable }.Concat(Arguments).Select(Quote));

    private static string Quote(string argument) =>
        argument.Length > 0 && !argument.Any(char.IsWhiteSpace) ? argument : $"\"{argument}\"";
}
