namespace Basics.Core;

/// <summary>
/// The subject of rows 13, 15 and 28. No test reaches it, and only their patches touch it.
/// </summary>
public static class Receipt
{
    public static string Header(string shop) => "Receipt: " + shop;
}
