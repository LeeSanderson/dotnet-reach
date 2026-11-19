namespace Reach.Changes;

/// <summary>
/// Every declaration of each type name on one side of a change, with the file it is declared
/// in. A partial type split across files has several.
/// </summary>
internal sealed class DeclarationsByTypeName()
    : Dictionary<string, List<(string Path, DeclaredType Type)>>(StringComparer.Ordinal)
{
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
            if (!TryGetValue(name, out var declarations))
            {
                this[name] = declarations = [];
            }

            declarations.Add((path, type));
        }

        return declared.Count > 0;
    }
}
