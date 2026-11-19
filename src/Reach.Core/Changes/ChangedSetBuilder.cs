using Reach.Git;
using Reach.Projects;
using Reach.Reporting;

namespace Reach.Changes;

/// <summary>
/// Turns a baseline SHA into the members a change added, altered or removed.
/// </summary>
/// <remarks>
/// <para>
/// Types are matched on <strong>fully-qualified name plus arity</strong>, across the whole
/// changed set rather than file by file, so a moved type is found on both sides and only its
/// genuinely changed members become roots. Git's similarity threshold and the pairing of a
/// delete with a specific add stop existing as concepts — which removes a tuning knob whose
/// wrong setting under-selects.
/// </para>
/// <para>
/// Simple name plus arity was rejected as the key: a namespace is part of the metadata name, so
/// a namespace move changes what runtime binding sees — serialization discriminators,
/// convention-based registration, <c>Type.GetType</c> — and those are blind spots Reach cannot
/// see into.
/// </para>
/// </remarks>
internal sealed class ChangedSetBuilder(GitAdapter git, string repositoryRoot, AnalysisScope scope)
{
    internal async Task<ChangedSet> BuildAsync(
        string baseline,
        CancellationToken cancellationToken = default)
    {
        var paths = await PathsAsync(baseline, cancellationToken).ConfigureAwait(false);

        var baselineTypes = new ChangeSide();
        var currentTypes = new ChangeSide();

        var unmappable = new List<ChangedPath>();
        var notices = new List<Notice>();
        var unparseable = new HashSet<string>(StringComparer.Ordinal);

        foreach (var path in paths)
        {
            if (!path.IsCSharp)
            {
                // A project file, an SDK bump, a resource. The tier ladder routes these, and
                // always widens.
                unmappable.Add(path);
                continue;
            }

            var declaredAnything = false;

            if (path.HasBaseline)
            {
                var text = await BaselineTextAsync(baseline, path.Path, cancellationToken).ConfigureAwait(false);
                declaredAnything |= baselineTypes.Collect(path.Path, text);
                Check(path, text, unparseable);
            }

            if (path.HasWorkingTree)
            {
                var text = WorkingTreeText(path.Path);
                declaredAnything |= currentTypes.Collect(path.Path, text);
                Check(path, text, unparseable);
            }

            // A source file that declares no type at all — global usings, assembly-level
            // attributes, top-level statements — cannot be routed through a type, so it goes
            // to the tier ladder instead of being silently dropped.
            if (!declaredAnything)
            {
                unmappable.Add(path);
            }
        }

        var compared = DeclarationComparison.Compare(baselineTypes, currentTypes);

        var assemblyWidenings = compared.DeletedTypes
            .Select(deleted => new AssemblyWidening(
                deleted.Name,
                WideningReason.DeletedType,
                deleted.Path,
                ProjectContaining(deleted.Path)))
            .ToList();

        NoteUntracked(paths, notices);
        NoteUnparseable(unparseable, notices);

        // A file Reach could not parse is a file whose members it cannot trust, so its whole
        // assembly widens rather than being read as unchanged.
        foreach (var path in unparseable.Order(StringComparer.Ordinal))
        {
            assemblyWidenings.Add(new AssemblyWidening(
                path,
                WideningReason.DeletedType,
                path,
                ProjectContaining(path)));
        }

        return new ChangedSet(compared.Members, compared.TypeWidenings, assemblyWidenings, paths, unmappable, notices);
    }

    /// <summary>
    /// Two commands, and between them they span everything: committed since the baseline,
    /// staged, unstaged and untracked.
    /// </summary>
    private async Task<IReadOnlyList<ChangedPath>> PathsAsync(
        string baseline,
        CancellationToken cancellationToken)
    {
        var diff = await git.ChangedPathsAsync(baseline, cancellationToken).ConfigureAwait(false);
        var untracked = await git.UntrackedPathsAsync(cancellationToken).ConfigureAwait(false);

        var paths = new Dictionary<string, ChangedPath>(StringComparer.Ordinal);

        foreach (var path in diff.Lines().Select(ChangedPath.FromNameStatus).OfType<ChangedPath>())
        {
            paths[path.Path] = path;
        }

        foreach (var path in untracked.Lines().Where(line => line.Length > 0))
        {
            paths.TryAdd(path, new ChangedPath(path, ChangeStatus.Untracked));
        }

        return [.. paths.Values.OrderBy(path => path.Path, StringComparer.Ordinal)];
    }

    private async Task<string> BaselineTextAsync(
        string baseline,
        string path,
        CancellationToken cancellationToken)
    {
        var result = await git.FileAtBaselineAsync(baseline, path, cancellationToken).ConfigureAwait(false);

        return result.Succeeded ? result.StandardOutput : string.Empty;
    }

    private string WorkingTreeText(string path)
    {
        try
        {
            return File.ReadAllText(Path.Combine(repositoryRoot, path));
        }
        catch (IOException)
        {
            // Deleted between git's answer and now. An empty revision reads as "declares
            // nothing", which routes the path to the tier ladder rather than losing it.
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Records a file Reach's parser did not fully understand. An error diagnostic or skipped
    /// tokens are both structural enough to distrust the members read out of the file — and
    /// skipped tokens especially, because a construct whose members are swallowed never puts a
    /// changed method into the changed set at all.
    /// </summary>
    private static void Check(ChangedPath path, string text, HashSet<string> unparseable)
    {
        if (!ParseHealthCheck.Of(text).IsSound)
        {
            unparseable.Add(path.Path);
        }
    }

    private static void NoteUnparseable(IReadOnlyCollection<string> unparseable, List<Notice> notices)
    {
        if (unparseable.Count == 0)
        {
            return;
        }

        var paths = unparseable.Order(StringComparer.Ordinal).ToArray();

        notices.Add(new Notice(
            NoticeCodes.ParseFailed,
            NoticeKind.BlindSpot,
            $"{paths.Length} changed file(s) did not parse cleanly, so their projects widened "
            + "rather than being read as unchanged: " + string.Join(", ", paths),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["paths"] = paths }));
    }

    /// <summary>
    /// The nearest ancestor project directory, which reproduces the SDK's default
    /// <c>**/*.cs</c> globbing without running MSBuild. Errs over — a path excluded by
    /// <c>&lt;Compile Remove&gt;</c> over-selects — and is imprecise only for linked files
    /// pulled in from outside the project directory.
    /// </summary>
    private ProjectFile? ProjectContaining(string repositoryRelativePath)
    {
        var full = Paths.Normalise(Path.Combine(repositoryRoot, repositoryRelativePath));

        return scope.Projects
            .Where(project => Paths.IsUnder(full, project.Directory))
            .OrderByDescending(project => project.Directory.Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// An untracked <c>.cs</c> file entering the changed set is worth a report line: it is
    /// either intentional codegen or a forgotten <c>git add</c>, and both bear on trusting a
    /// surprising selection.
    /// </summary>
    private static void NoteUntracked(IReadOnlyList<ChangedPath> paths, List<Notice> notices)
    {
        var untracked = paths
            .Where(path => path.Status == ChangeStatus.Untracked && path.IsCSharp)
            .Select(path => path.Path)
            .ToArray();

        if (untracked.Length == 0)
        {
            return;
        }

        notices.Add(new Notice(
            NoticeCodes.UntrackedSourceFiles,
            NoticeKind.Scope,
            $"{untracked.Length} untracked C# file(s) were analysed as changes: "
            + $"{string.Join(", ", untracked)}. Either intentional code generation, or a "
            + "forgotten `git add`.",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["paths"] = untracked }));
    }
}
