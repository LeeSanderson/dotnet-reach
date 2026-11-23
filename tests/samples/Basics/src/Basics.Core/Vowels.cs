namespace Basics.Core;

/// <summary>
/// Row 11's subject: a missing runner setting gets its notice. Only row 11's change patch
/// touches it.
/// </summary>
public static class Vowels
{
    private const string All = "aeiouAEIOU";

    public static int In(string text) => text.Count(All.Contains);
}
