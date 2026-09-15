using System.Runtime.InteropServices;
using Reach.Processes;

namespace Reach.Tests.Fixtures;

/// <summary>
/// Small child processes the port's own tests need and no real tool provides: one that
/// floods both output streams, and one that spawns a grandchild and then never exits.
/// Written as a platform script rather than a helper assembly so there is nothing to build.
/// </summary>
internal sealed class ChildScript : IDisposable
{
    private static readonly bool OnWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    private readonly TempDirectory directory;
    private readonly string scriptPath;

    private ChildScript(TempDirectory directory, string scriptPath)
    {
        this.directory = directory;
        this.scriptPath = scriptPath;
    }

    /// <summary>Writes <paramref name="lines"/> long lines to standard output and the same to standard error.</summary>
    internal static ChildScript Chatty() => Write("chatty", ChattyBody);

    /// <summary>
    /// Appends a line to its own heartbeat file every second, having first started a
    /// grandchild doing the same to a second file. Never exits on its own.
    /// </summary>
    internal static ChildScript Spawner() => Write("spawner", SpawnerBody);

    /// <summary>The argument vector that runs this script, through the platform's shell.</summary>
    internal ProcessRequest Invoke(params string[] arguments) =>
        new(
            OnWindows ? "cmd.exe" : "/bin/sh",
            OnWindows ? ["/c", scriptPath, .. arguments] : [scriptPath, .. arguments],
            directory.Path);

    private static ChildScript Write(string name, string body)
    {
        var directory = TempDirectory.Create($"reach-{name}");
        var scriptPath = directory.Combine(OnWindows ? $"{name}.cmd" : $"{name}.sh");

        // LF only: a .sh file with CRLF line endings fails with "bad interpreter".
        File.WriteAllText(scriptPath, body.ReplaceLineEndings("\n"));

        // OperatingSystem.IsWindows() rather than the OnWindows field: the platform-
        // compatibility analyzer only recognises the call as a guard.
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                scriptPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        return new ChildScript(directory, scriptPath);
    }

    // Each line is long enough that a few hundred of them exceed any pipe buffer, which is
    // the point: a runner that drains one stream at a time deadlocks here rather than failing.
    private const string Padding = "0123456789012345678901234567890123456789012345678901234567890123";

    private static string ChattyBody => OnWindows
        ? $"""
          @echo off
          for /L %%i in (1,1,%1) do (
            echo out %%i {Padding}
            echo err %%i {Padding} 1>&2
          )
          """
        : $"""
          i=0
          while [ "$i" -lt "$1" ]; do
            echo "out $i {Padding}"
            echo "err $i {Padding}" >&2
            i=$((i + 1))
          done
          """;

    // Called with two arguments it spawns itself with one, so the same file is both the
    // child and the grandchild. Called with one it only heartbeats.
    private static string SpawnerBody => OnWindows
        ? """
          @echo off
          rem The doubled quotes are cmd's, not a typo: start reads the first quoted token as
          rem a window title, so the whole child command line needs its own pair around it.
          if not "%~2"=="" start "" /b cmd.exe /c ""%~f0" "%~2""
          :loop
          echo tick>>"%~1"
          ping -n 2 127.0.0.1 >nul
          goto loop
          """
        : """
          if [ -n "$2" ]; then
            /bin/sh "$0" "$2" &
          fi
          while :; do
            echo tick >> "$1"
            sleep 1
          done
          """;

    public void Dispose() => directory.Dispose();
}
