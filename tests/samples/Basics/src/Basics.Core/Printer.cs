namespace Basics.Core;

/// <summary>
/// Row 4: calls <see cref="Label.Render"/> on the base type, so a change to either override
/// reaches every test that prints through it.
/// </summary>
public static class Printer
{
    public static string Print(Label label, string text) => label.Render(text);
}
