namespace Reach.Changes;

/// <summary>
/// Matches the types declared at the baseline with those declared in the working tree, by
/// fully-qualified name plus arity, and reads off what changed: the members added, altered or
/// removed, the types that widen as a whole, and the types that no longer exist.
/// </summary>
internal static class DeclarationComparison
{
    internal static DeclarationChanges Compare(DeclarationsByTypeName baseline, DeclarationsByTypeName current)
    {
        var members = new List<ChangedMember>();
        var typeWidenings = new List<TypeWidening>();
        var deletedTypes = new List<(string Name, string Path)>();

        foreach (var name in baseline.Keys.Concat(current.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var before = baseline.GetValueOrDefault(name);
            var after = current.GetValueOrDefault(name);

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

    private static void CompareType(
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
    /// Partial declarations in source order, which is deterministic for a fixed path order.
    /// </summary>
    private static string Header(List<(string Path, DeclaredType Type)> declarations) =>
        string.Join(
            '\u0001',
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
