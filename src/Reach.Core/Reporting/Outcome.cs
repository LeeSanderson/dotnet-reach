namespace Reach.Reporting;

/// <summary>
/// What a run concluded, from a closed set. An empty selection and an empty change set are
/// different news and are never conflated.
/// </summary>
/// <remarks>
/// There is deliberately no "everything was selected" outcome — it is derivable from the
/// entries' modes, and a second way to say it is a second thing to keep consistent.
/// </remarks>
internal enum Outcome
{
    Selected,
    NothingSelected,
    NoChanges,
    Failed,
}

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
