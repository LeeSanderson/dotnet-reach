namespace Reach.Changes;

/// <summary>
/// Matches the types declared at the baseline with those declared in the working tree, by
/// fully-qualified name plus arity, and reads off what changed: the members added, altered or
/// removed, the types that widen as a whole, and the types that no longer exist.
/// </summary>
internal static class DeclarationComparison
{
    internal static DeclarationChanges Compare(ChangeSide baseline, ChangeSide current)
    {
        var members = new List<ChangedMember>();
        var typeWidenings = new List<TypeWidening>();
        var deletedTypes = new List<(string Name, string Path)>();

        foreach (var name in baseline.Names.Concat(current.Names).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var before = baseline.DeclarationsOf(name);
            var after = current.DeclarationsOf(name);

            if (before is null)
            {
                AddedType(after!, name, members);
            }
            else if (after is null)
            {
                deletedTypes.Add((name, before[0].Path));
            }
            else
            {
                CompareType(name, before, after, members, typeWidenings);
            }
        }

        return new DeclarationChanges(members, typeWidenings, deletedTypes);
    }

    /// <summary>
    /// An added type has no baseline, so every member it declares is a root.
    /// </summary>
    private static void AddedType(
        IReadOnlyList<TypeDeclaration> declarations,
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

    private static void CompareType(
        string name,
        IReadOnlyList<TypeDeclaration> before,
        IReadOnlyList<TypeDeclaration> after,
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

        var baselineType = Combined(before);
        var currentType = Combined(after);

        if (!string.Equals(baselineType.Header, currentType.Header, StringComparison.Ordinal))
        {
            typeWidenings.Add(new TypeWidening(name, WideningReason.TypeHeaderChanged, path));
        }

        var baselineMembers = baselineType.Members;
        var currentMembers = currentType.Members;

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
            else
            {
                // Present on both sides with the same declaration: unchanged.
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

    /// <summary>
    /// One side's declarations of a type as one type, in file path order: each file's own
    /// declarations are already combined in source order, so the whole is deterministic.
    /// </summary>
    private static DeclaredType Combined(IReadOnlyList<TypeDeclaration> declarations) =>
        DeclaredType.Combine(
            [.. declarations
                .OrderBy(declaration => declaration.Path, StringComparer.Ordinal)
                .Select(declaration => declaration.Type)]);
}

/// <summary>
/// What comparing the two sides' declarations found.
/// </summary>
/// <param name="Members">The members added, altered or removed.</param>
/// <param name="TypeWidenings">The types that widen as a whole: moved, re-headed, or missing a member.</param>
/// <param name="DeletedTypes">
/// The types the baseline declares and the working tree does not, with the file the baseline
/// declared each in. No identity in the current binaries at all, so there is nothing for the
/// reverse walk to start from.
/// </param>
internal sealed record DeclarationChanges(
    IReadOnlyList<ChangedMember> Members,
    IReadOnlyList<TypeWidening> TypeWidenings,
    IReadOnlyList<(string Name, string Path)> DeletedTypes);
