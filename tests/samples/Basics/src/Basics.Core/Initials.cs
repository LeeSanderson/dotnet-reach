namespace Basics.Core;

/// <summary>
/// Row 2's subject: the tests of it change, it does not. Only row 2's patch touches its tests.
/// </summary>
public static class Initials
{
    public static string Of(string fullName) =>
        string.Concat(fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(name => name[0]));
}
