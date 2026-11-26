namespace Basics.Core;

/// <summary>
/// Row 25's subject, a known gap: its <see cref="ToString"/> is reached only through string
/// interpolation, which calls it from the BCL, so no compiled edge leads a test to it.
/// Gap-shaped: no gate patch touches it.
/// </summary>
public sealed class Temperature(int degrees)
{
    public override string ToString() => $"{degrees} C";
}
