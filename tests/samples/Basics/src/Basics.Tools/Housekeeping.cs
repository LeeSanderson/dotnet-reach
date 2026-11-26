namespace Basics.Tools;

/// <summary>
/// Row 29's subject. No test project references this project, so it is outside the analysis
/// scope, and only row 29's patch touches it.
/// </summary>
public static class Housekeeping
{
    public static int DaysToKeep(int days) => Math.Max(days, 1);
}
