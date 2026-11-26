// A file-based app defaults to native AOT, which pulls in the ILCompiler package; the tool only
// ever runs through dotnet run, so it needs none of it.
#:property PublishAot=false
#:property RootNamespace=Reach.Tools

// Writes and applies the acceptance layer's patches. The design is §3 of the acceptance-layer spec:
// https://github.com/LeeSanderson/dotnet-reach/blob/83b450b13dd5cd701c5f9939f97519a50a9645bc/.scratch/acceptance-layer/spec.md#3-patches
//
// Run from anywhere in the repository:
//
//   dotnet run tests/tools/patch.cs -- save  <Sample> <name> [--on <baseline>] [--force]
//   dotnet run tests/tools/patch.cs -- apply <Sample> <name> [--on <baseline>]
//
// <Sample> is a folder under tests/samples/. <name> and <baseline> are written without .patch.
// Patches live in tests/Reach.Tests/Acceptance/<Sample>/.
using System.Diagnostics;
using System.Text;
using Reach.Tools;

return PatchTool.Run(args);

namespace Reach.Tools
{
    /// <summary>
    /// The two commands, and the usage and error reporting around them.
    /// </summary>
    internal static class PatchTool
    {
        private const int Failed = 1;

        private const string Usage =
            "usage:\n"
            + "  dotnet run tests/tools/patch.cs -- save  <Sample> <name> [--on <baseline>] [--force]\n"
            + "  dotnet run tests/tools/patch.cs -- apply <Sample> <name> [--on <baseline>]";

        internal static int Run(string[] args)
        {
            try
            {
                var command = Command.Parse(args);
                var workspace = Workspace.Open(command.Sample);

                if (command.IsSave)
                {
                    Saver.Save(workspace, command);
                }
                else
                {
                    Applier.Apply(workspace, command);
                }

                return 0;
            }
            catch (UsageException exception)
            {
                Console.Error.WriteLine($"patch: {exception.Message}\n{Usage}");
                return Failed;
            }
            catch (PatchException exception)
            {
                Console.Error.WriteLine($"patch: {exception.Message}");
                return Failed;
            }
        }
    }

    /// <summary>
    /// The parsed command line.
    /// </summary>
    internal sealed record Command(bool IsSave, string Sample, string Name, string? On, bool Force)
    {
        internal static Command Parse(string[] args)
        {
            var (positional, on, force) = Split(args);

            if (!positional.TryDequeue(out var verb)
                || !positional.TryDequeue(out var sample)
                || !positional.TryDequeue(out var name)
                || positional.Count > 0)
            {
                throw new UsageException("expected a command, a sample and a patch name.");
            }

            var isSave = verb switch
            {
                "save" => true,
                "apply" => false,
                _ => throw new UsageException($"unknown command {verb}."),
            };

            if (force && !isSave)
            {
                throw new UsageException("--force is for save only.");
            }

            return new Command(isSave, sample, WithoutExtension(name), on, force);
        }

        /// <summary>
        /// The positional arguments in order, and the two options.
        /// </summary>
        private static (Queue<string> Positional, string? On, bool Force) Split(string[] args)
        {
            var positional = new Queue<string>();
            string? on = null;
            var force = false;
            var remaining = new Queue<string>(args);

            while (remaining.TryDequeue(out var argument))
            {
                switch (argument)
                {
                    case "--on":
                        on = remaining.TryDequeue(out var baseline)
                            ? WithoutExtension(baseline)
                            : throw new UsageException("--on needs a baseline patch name.");
                        break;
                    case "--force":
                        force = true;
                        break;
                    case var option when option.StartsWith("--", StringComparison.Ordinal):
                        throw new UsageException($"unknown option {option}.");
                    default:
                        positional.Enqueue(argument);
                        break;
                }
            }

            return (positional, on, force);
        }

        private static string WithoutExtension(string name) =>
            name.EndsWith(".patch", StringComparison.OrdinalIgnoreCase) ? name[..^".patch".Length] : name;
    }

    /// <summary>
    /// Where a sample and its patches are, found from the repository root.
    /// </summary>
    internal sealed class Workspace
    {
        private Workspace(string root, string sample)
        {
            Root = root;
            Sample = sample;
            SamplePath = "tests/samples/" + sample;
            PatchFolder = Path.Combine(root, "tests", "Reach.Tests", "Acceptance", sample);
        }

        internal string Root { get; }

        internal string Sample { get; }

        /// <summary>
        /// The sample's folder from the repository root, with forward slashes, as git takes it.
        /// </summary>
        internal string SamplePath { get; }

        internal string PatchFolder { get; }

        internal static Workspace Open(string sample)
        {
            var root = Git.Run(Environment.CurrentDirectory, ["rev-parse", "--show-toplevel"]).Trim();
            var samples = Path.Combine(root, "tests", "samples");

            if (!Directory.Exists(Path.Combine(samples, sample)))
            {
                var known = Directory.Exists(samples)
                    ? string.Join(", ", Directory.EnumerateDirectories(samples).Select(Path.GetFileName).Order(StringComparer.Ordinal))
                    : "none";

                throw new PatchException($"tests/samples/ has no folder {sample}. Its samples are: {known}.");
            }

            return new Workspace(root, sample);
        }

        /// <summary>
        /// The path of a patch by name, which must exist.
        /// </summary>
        internal string ExistingPatch(string name)
        {
            var path = PatchPath(name);

            return File.Exists(path)
                ? path
                : throw new PatchException($"no patch named '{name}' at {Relative(path)}.");
        }

        internal string PatchPath(string name) => Path.Combine(PatchFolder, name + ".patch");

        internal string Relative(string path) => Path.GetRelativePath(Root, path).Replace('\\', '/');

        /// <summary>
        /// A patch's text as git reads it inside the repository: LF line endings.
        /// </summary>
        /// <remarks>
        /// Patches and samples are checked out with CRLF (ADR-0017), but git compares a patch with the
        /// clean form of each file, which <c>.gitattributes</c> makes LF on every platform.
        /// </remarks>
        internal static string ReadForGit(string path) => File.ReadAllText(path).ReplaceLineEndings("\n");
    }

    /// <summary>
    /// <c>save</c>: the sample's uncommitted edits, as a stripped patch, then the sample reverted.
    /// </summary>
    internal static class Saver
    {
        internal static void Save(Workspace workspace, Command command)
        {
            var target = workspace.PatchPath(command.Name);

            if (File.Exists(target) && !command.Force)
            {
                throw new PatchException($"{workspace.Relative(target)} already exists. Pass --force to overwrite it.");
            }

            var baseline = command.On is null ? null : workspace.ExistingPatch(command.On);
            var patch = Stripped.From(workspace, Diff(workspace, baseline));

            if (patch.Length == 0)
            {
                throw new PatchException(
                    baseline is null
                        ? $"nothing to save: {workspace.SamplePath}/ has no uncommitted edits."
                        : $"nothing to save: {workspace.SamplePath}/ has no edits beyond {command.On}.patch.");
            }

            Directory.CreateDirectory(workspace.PatchFolder);

            try
            {
                // CRLF, as git checks the patch out (ADR-0017), so the file is the same before and
                // after a commit and the harness applies it byte for byte on every platform.
                File.WriteAllText(target, patch.ReplaceLineEndings("\r\n"), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                throw new PatchException($"could not write {workspace.Relative(target)}, so the sample was left as it is: {exception.Message}", exception);
            }

            Revert(workspace);

            Console.WriteLine($"wrote {workspace.Relative(target)} and reverted {workspace.SamplePath}/.");
        }

        /// <summary>
        /// The working tree under the sample against the committed sample, with the baseline patch
        /// applied first when there is one. Untracked files count as created.
        /// </summary>
        /// <remarks>
        /// Done in a throwaway index, so the developer's own index is never touched: it starts as
        /// <c>HEAD</c>, takes the baseline patch, and records every untracked file as intent-to-add.
        /// </remarks>
        private static string Diff(Workspace workspace, string? baseline)
        {
            var directory = Directory.CreateTempSubdirectory("reach-patch-");

            try
            {
                var index = new Dictionary<string, string> { ["GIT_INDEX_FILE"] = Path.Combine(directory.FullName, "index") };

                Git.Run(workspace.Root, ["read-tree", "HEAD"], index);

                if (baseline is not null)
                {
                    Git.Run(workspace.Root, ["apply", "--cached", $"--directory={workspace.SamplePath}", "-"], index, Workspace.ReadForGit(baseline));
                }

                var untracked = Git.Run(workspace.Root, ["ls-files", "-z", "--others", "--exclude-standard", "--", workspace.SamplePath], index)
                    .Split('\0', StringSplitOptions.RemoveEmptyEntries);

                if (untracked.Length > 0)
                {
                    Git.Run(workspace.Root, ["add", "--intent-to-add", "--", .. untracked], index);
                }

                return Git.Run(
                    workspace.Root,
                    [
                        "-c", "core.quotePath=false",
                        "diff", "--no-color", "--no-ext-diff", "--no-textconv", "--no-renames",
                        "--src-prefix=a/", "--dst-prefix=b/", $"--relative={workspace.SamplePath}/",
                        "--", workspace.SamplePath,
                    ],
                    index);
            }
            finally
            {
                directory.Delete(recursive: true);
            }
        }

        /// <summary>
        /// Puts the sample back as committed: tracked files restored, staged changes dropped, and
        /// untracked files removed. Ignored files, such as build output, stay.
        /// </summary>
        private static void Revert(Workspace workspace)
        {
            Git.Run(workspace.Root, ["restore", "--source=HEAD", "--staged", "--worktree", "--", workspace.SamplePath]);
            Git.Run(workspace.Root, ["clean", "-fdq", "--", workspace.SamplePath]);
        }
    }

    /// <summary>
    /// <c>git diff</c> output reduced to the checked-in form (acceptance-layer spec §3.1): only
    /// <c>---</c>/<c>+++</c> file headers and <c>@@</c> hunks.
    /// </summary>
    internal static class Stripped
    {
        private const string FileStart = "diff --git ";

        internal static string From(Workspace workspace, string diff)
        {
            var output = new StringBuilder();
            var lines = diff.ReplaceLineEndings("\n").Split('\n');
            var start = 0;

            while (start < lines.Length)
            {
                var end = start + 1;

                while (end < lines.Length && !lines[end].StartsWith(FileStart, StringComparison.Ordinal))
                {
                    end++;
                }

                AppendFile(workspace, lines[start..end], output);
                start = end;
            }

            return output.ToString();
        }

        /// <summary>
        /// One file's diff from its <c>---</c> header on. A file with no hunks (a mode change, an empty
        /// file) or binary content has no stripped form, so it stops the save before anything is
        /// written or reverted.
        /// </summary>
        private static void AppendFile(Workspace workspace, string[] lines, StringBuilder output)
        {
            if (lines.Length == 0 || !lines[0].StartsWith(FileStart, StringComparison.Ordinal))
            {
                return;
            }

            var header = Array.FindIndex(lines, line => line.StartsWith("--- ", StringComparison.Ordinal));

            if (header < 0)
            {
                var binary = Array.Exists(lines, line => line.StartsWith("Binary files ", StringComparison.Ordinal));

                throw new PatchException(
                    $"{lines[0][FileStart.Length..]} under {workspace.SamplePath}/ is "
                    + (binary ? "binary" : "a change with no lines (a mode change or an empty file)")
                    + ", which a stripped patch cannot carry. Undo it, or put a line in the file, and save again.");
            }

            foreach (var line in lines[header..])
            {
                if (line.Length > 0)
                {
                    output.Append(line).Append('\n');
                }
            }
        }
    }

    /// <summary>
    /// <c>apply</c>: a patch applied to the sample in the working tree, the baseline first.
    /// </summary>
    /// <remarks>
    /// <c>--3way</c> stages what it applies, and leaves conflict markers for a human to resolve when
    /// git has the blobs to merge. The harness never uses it (acceptance-layer spec §3.1).
    /// </remarks>
    internal static class Applier
    {
        internal static void Apply(Workspace workspace, Command command)
        {
            var patches = command.On is null
                ? [workspace.ExistingPatch(command.Name)]
                : new[] { workspace.ExistingPatch(command.On), workspace.ExistingPatch(command.Name) };

            foreach (var patch in patches)
            {
                var applied = Git.Try(workspace.Root, ["apply", "--3way", "--verbose", $"--directory={workspace.SamplePath}", "-"], input: Workspace.ReadForGit(patch));

                Console.WriteLine(applied.Said);

                if (applied.ExitCode != 0)
                {
                    // A stripped patch records no blob ids, so git has nothing to merge with and
                    // --3way falls back to a plain apply that either fits or fails.
                    throw new PatchException(
                        $"{workspace.Relative(patch)} did not apply, and a stripped patch cannot be merged with conflict "
                        + "markers. Make its change by hand in the sample, using the context git searched for above, then save it.");
                }

                Console.WriteLine($"applied {workspace.Relative(patch)} to {workspace.SamplePath}/.");
            }

            Console.WriteLine(
                $"Edit the sample, then: dotnet run tests/tools/patch.cs -- save {workspace.Sample} {command.Name}"
                + (command.On is null ? string.Empty : $" --on {command.On}")
                + " --force");
        }
    }

    /// <summary>
    /// Runs git and returns its standard output, or fails with everything it said.
    /// </summary>
    internal static class Git
    {
        /// <summary>
        /// The git on <c>PATH</c>, resolved once to an absolute path so the tool runs exactly the
        /// binary it names in its errors.
        /// </summary>
        private static readonly Lazy<string> Executable = new(Resolve);

        internal static string Run(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment = null,
            string? input = null)
        {
            var result = Try(workingDirectory, arguments, environment, input);

            return result.ExitCode == 0
                ? result.Output
                : throw new PatchException($"git {string.Join(' ', arguments)} failed:\n{result.Said}");
        }

        /// <summary>
        /// Runs git and returns its exit code, its standard output, and everything it said.
        /// </summary>
        internal static (int ExitCode, string Output, string Said) Try(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string>? environment = null,
            string? input = null)
        {
            var start = new ProcessStartInfo(Executable.Value)
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardInput = input is not null,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = input is null ? null : new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                UseShellExecute = false,
            };

            foreach (var argument in arguments)
            {
                start.ArgumentList.Add(argument);
            }

            foreach (var (name, value) in environment ?? new Dictionary<string, string>())
            {
                start.Environment[name] = value;
            }

            using var process = Process.Start(start)
                ?? throw new PatchException("could not start git.");

            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();

            if (input is not null)
            {
                process.StandardInput.Write(input);
                process.StandardInput.Close();
            }

            process.WaitForExit();

            return (process.ExitCode, output.Result, (output.Result + error.Result).TrimEnd());
        }

        private static string Resolve()
        {
            var names = OperatingSystem.IsWindows() ? new[] { "git.exe", "git.cmd" } : ["git"];
            var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return directories
                .Where(Path.IsPathFullyQualified)
                .SelectMany(directory => names.Select(name => Path.Combine(directory, name)))
                .FirstOrDefault(File.Exists)
                ?? throw new PatchException("git is not on PATH.");
        }
    }

    /// <summary>
    /// A failure the tool reports and stops on.
    /// </summary>
    public class PatchException : Exception
    {
        public PatchException()
        {
        }

        public PatchException(string message)
            : base(message)
        {
        }

        public PatchException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    /// <summary>
    /// A command line the tool cannot read.
    /// </summary>
    public sealed class UsageException : PatchException
    {
        public UsageException()
        {
        }

        public UsageException(string message)
            : base(message)
        {
        }

        public UsageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
