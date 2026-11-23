namespace Basics.Core;

/// <summary>
/// Row 4: the other override of <see cref="Label.Render"/>.
/// </summary>
public sealed class BracketLabel : Label
{
    public override string Render(string text) => $"[{text}]";
}
