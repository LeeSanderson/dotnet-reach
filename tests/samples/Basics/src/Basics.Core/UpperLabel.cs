namespace Basics.Core;

/// <summary>
/// Row 4: one override of <see cref="Label.Render"/>.
/// </summary>
public sealed class UpperLabel : Label
{
    public override string Render(string text) => text.ToUpperInvariant();
}
