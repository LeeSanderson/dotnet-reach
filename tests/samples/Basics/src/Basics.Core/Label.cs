namespace Basics.Core;

/// <summary>
/// Row 4's virtual hierarchy: a base class with two overrides, and a caller,
/// <see cref="Printer"/>, that sees only the base. Only row 4's patch touches this hierarchy.
/// </summary>
public class Label
{
    public virtual string Render(string text) => text;
}
