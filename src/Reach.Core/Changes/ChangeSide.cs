namespace Reach.Changes;

/// <summary>
/// One side of a change, the baseline or the working tree, read as the types it declares:
/// every declaration of each type name, with the file it is declared in. A partial type split
/// across files has several.
/// </summary>
internal sealed class ChangeSide
{
    private readonly Dictionary<string, List<TypeDeclaration>> _byName = new(StringComparer.Ordinal);

    /// <summary>
    /// Every type name this side declares.
    /// </summary>
    internal IEnumerable<string> Names => _byName.Keys;

    /// <summary>
    /// Adds every type one revision of one file declares. False when it declares none, which
    /// includes a revision that does not exist.
    /// </summary>
    internal bool Collect(string path, string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        var declared = SourceRevision.DeclaredTypes(text);

        foreach (var (name, type) in declared)
        {
            Buckets.Bucket(_byName, name).Add(new TypeDeclaration(path, type));
        }

        return declared.Count > 0;
    }

    /// <summary>
    /// Every declaration of <paramref name="name"/>, in the order the files were collected, or
    /// null when this side does not declare it.
    /// </summary>
    internal IReadOnlyList<TypeDeclaration>? DeclarationsOf(string name) => _byName.GetValueOrDefault(name);
}

/// <summary>
/// One declaration of a type, and the repository-relative file it is declared in.
/// </summary>
/// <param name="Path">The file, which decides which project compiles the type.</param>
/// <param name="Type">What that file declares of the type.</param>
internal sealed record TypeDeclaration(string Path, DeclaredType Type);
