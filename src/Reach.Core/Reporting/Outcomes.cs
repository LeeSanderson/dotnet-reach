namespace Reach.Reporting;

internal static class Outcomes
{
    internal static string Wire(this Outcome outcome) => outcome switch
    {
        Outcome.Selected => "selected",
        Outcome.NothingSelected => "nothing-selected",
        Outcome.NoChanges => "no-changes",
        _ => "failed",
    };
}
