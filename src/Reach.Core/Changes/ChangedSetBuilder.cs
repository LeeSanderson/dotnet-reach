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

        var baselineTypes = new Dictionary<string, List<(string Path, DeclaredType Type)>>(StringComparer.Ordinal);
        var currentTypes = new Dictionary<string, List<(string Path, DeclaredType Type)>>(StringComparer.Ordinal);

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
                declaredAnything |= Collect(baselineTypes, path.Path, text);
                Check(path, text, unparseable);
            }

            if (path.HasWorkingTree)
            {
                var text = WorkingTreeText(path.Path);
                declaredAnything |= Collect(currentTypes, path.Path, text);
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

        var members = new List<ChangedMember>();
        var typeWidenings = new List<TypeWidening>();
        var assemblyWidenings = new List<AssemblyWidening>();

        foreach (var name in baselineTypes.Keys.Concat(currentTypes.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var before = baselineTypes.GetValueOrDefault(name);
            var after = currentTypes.GetValueOrDefault(name);

            if (before is null)
            {
                AddedType(after!, name, members);
            }
            else if (after is null)
            {
                // No identity in the current binaries at all, so there is nothing for the
                // reverse walk to start from.
                assemblyWidenings.Add(new AssemblyWidening(
                    name,
                    WideningReason.DeletedType,
                    before[0].Path,
                    ProjectContaining(before[0].Path)));
            }
            else
            {
                Compare(name, before, after, members, typeWidenings);
            }
        }

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

        return new ChangedSet(members, typeWidenings, assemblyWidenings, paths, unmappable, notices);
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

        foreach (var path in diff.Lines.Select(ChangedPath.FromNameStatus).OfType<ChangedPath>())
        {
            paths[path.Path] = path;
        }

        foreach (var path in untracked.Lines.Where(line => line.Length > 0))
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

    private static bool Collect(
        Dictionary<string, List<(string Path, DeclaredType Type)>> into,
        string path,
        string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        var declared = SourceRevision.DeclaredTypes(text);

        foreach (var (name, type) in declared)
        {
            if (!into.TryGetValue(name, out var declarations))
            {
                into[name] = declarations = [];
            }

            declarations.Add((path, type));
        }

        return declared.Count > 0;
    }

    /// <summary>An added type has no baseline, so every member it declares is a root.</summary>
    private static void AddedType(
        List<(string Path, DeclaredType Type)> declarations,
        string name,
        List<ChangedMember> members)
    {
        foreach (var (path, type) in declarations)
        {
            foreach (var member in type.Members.Values)
            {
                members.Add(new ChangedMember(
                    name,
                    member.Key,
                    MemberChange.Added,
                    path,
                    member.IsCompileTimeConstant,
                    member.Span));
            }
        }
    }

    private void Compare(
        string name,
        List<(string Path, DeclaredType Type)> before,
        List<(string Path, DeclaredType Type)> after,
        List<ChangedMember> members,
        List<TypeWidening> typeWidenings)
    {
        var baselinePaths = before.Select(entry => entry.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var currentPaths = after.Select(entry => entry.Path).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var path = after[0].Path;

        // The file a type lives in decides which project compiles it, so a directory-only
        // move can move the type between assemblies without changing a line of its source.
        if (!baselinePaths.SequenceEqual(currentPaths, StringComparer.Ordinal))
        {
            typeWidenings.Add(new TypeWidening(name, WideningReason.TypeMoved, path));
        }

        if (!string.Equals(Header(before), Header(after), StringComparison.Ordinal))
        {
            typeWidenings.Add(new TypeWidening(name, WideningReason.TypeHeaderChanged, path));
        }

        var baselineMembers = Merge(before);
        var currentMembers = Merge(after);

        foreach (var (key, member) in currentMembers)
        {
            if (!baselineMembers.TryGetValue(key, out var previous))
            {
                members.Add(new ChangedMember(
                    name, key, MemberChange.Added, path, member.IsCompileTimeConstant, member.Span));
            }
            else if (!string.Equals(previous.Declaration, member.Declaration, StringComparison.Ordinal))
            {
                members.Add(new ChangedMember(
                    name,
                    key,
                    MemberChange.Modified,
                    path,
                    member.IsCompileTimeConstant || previous.IsCompileTimeConstant,
                    member.Span));
            }
        }

        var removed = baselineMembers.Where(entry => !currentMembers.ContainsKey(entry.Key)).ToArray();

        foreach (var (key, member) in removed)
        {
            members.Add(new ChangedMember(
                name, key, MemberChange.Removed, before[0].Path, member.IsCompileTimeConstant, member.Span));
        }

        if (removed.Length > 0)
        {
            // Unconditional. A deletion can rebind rather than remove a binding, and the
            // absorption test that would prove most deletions inert is a proof in five
            // clauses, each a place to be wrong in the under-selecting direction.
            typeWidenings.Add(new TypeWidening(name, WideningReason.DeletedMember, path));
        }
    }

    /// <summary>Partial declarations in source order, which is deterministic for a fixed path order.</summary>
    private static string Header(List<(string Path, DeclaredType Type)> declarations) =>
        string.Join(
            '',
            declarations
                .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                .Select(entry => entry.Type.Header));

    private static Dictionary<MemberKey, DeclaredMember> Merge(
        List<(string Path, DeclaredType Type)> declarations)
    {
        var merged = new Dictionary<MemberKey, DeclaredMember>();

        foreach (var (_, type) in declarations.OrderBy(entry => entry.Path, StringComparer.Ordinal))
        {
            foreach (var (key, member) in type.Members)
            {
                merged[key] = member;
            }
        }

        return merged;
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
            NoticeCodes.UntrackedSourceInChangeSet,
            NoticeKind.Scope,
            $"{untracked.Length} untracked C# file(s) were analysed as changes: "
            + $"{string.Join(", ", untracked)}. Either intentional code generation, or a "
            + "forgotten `git add`.",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["paths"] = untracked }));
    }
}
