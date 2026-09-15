using Reach.Graph;
using Reach.Projects;
using Reach.Reporting;

namespace Reach.Selection;

/// <summary>
/// Why a test was selected. The rules are not interchangeable and only the first is an
/// analysis result, so each selected test records which of them applied.
/// </summary>
internal enum SelectionRule
{
    /// <summary>Reachable backwards along call edges from a change somewhere else.</summary>
    ReverseReachable,

    /// <summary>Its own declaration changed.</summary>
    OwnSourceChanged,

    /// <summary>
    /// Its declaration was added. Derived from the changed set rather than by comparing two
    /// test lists — Reach reads only the current compiled output and cannot enumerate the
    /// baseline's tests without building it.
    /// </summary>
    NewSinceBaseline,
}

/// <summary>What a test project is going to be asked to run.</summary>
internal enum SelectionMode
{
    /// <summary>Some of its tests, named by a filter.</summary>
    Filtered,

    /// <summary>All of them, because Reach could not analyse the project.</summary>
    RunAll,

    /// <summary>None of them. An empty filter would run everything, so nothing is emitted at all.</summary>
    Skip,
}

/// <summary>How a change was attributed. The tier lives on the change, not on the pair.</summary>
internal enum ChangeTier
{
    /// <summary>Attributed to one or more methods.</summary>
    Member,

    /// <summary>Every surviving member of a type.</summary>
    WholeType,

    /// <summary>Every method of an assembly.</summary>
    WholeAssembly,
}

/// <summary>
/// One change, with the tier that routed it and the count of tests it reached.
/// </summary>
/// <remarks>
/// <strong>The change side is keyed on changes, not on expanded roots.</strong> Whole-assembly
/// widening puts every method of an assembly into the changed set, so one unattributable file
/// would otherwise expand into ten thousand roots and every test in that assembly's dependents
/// would carry all of them. A whole-assembly widening contributes <em>one</em> entry here; the
/// expansion stays an implementation detail of the walk.
/// <para>
/// Counts only, never test names — the pairs live once, on the test side. It is arguably the
/// most valuable field in the report: it makes "I changed <c>OrderService.Submit</c> and
/// nothing runs" a line you read rather than an absence you have to notice.
/// </para>
/// </remarks>
internal sealed record ChangeEntry(int Index, string Display, ChangeTier Tier, string Reason)
{
    internal int TestsReached { get; set; }
}

/// <param name="Changes">Indices into the forward change list. Empty exactly when the test is not reverse-reachable.</param>
/// <param name="Class">Null exactly when the test is not reverse-reachable.</param>
/// <param name="Paths">Hop-by-hop, only under <c>--paths</c>.</param>
internal sealed record SelectedTest(
    TestMethod Test,
    IReadOnlyList<SelectionRule> Rules,
    IReadOnlyList<int> Changes,
    PathClass? Class,
    IReadOnlyList<IReadOnlyList<MethodId>>? Paths = null);

/// <summary>
/// One entry of the report, keyed on (test project, target framework).
/// </summary>
/// <param name="Total">
/// Null means <em>unknown</em>, never zero. Reporting zero for a project Reach could not
/// analyse would silently corrupt the over-selection ratio in exactly the case where Reach is
/// running an entire project.
/// </param>
internal sealed record ProjectSelection(
    ProjectFile Project,
    string TargetFramework,
    SelectionMode Mode,
    IReadOnlyList<SelectedTest> Selected,
    int? Total,
    string? Dialect);

/// <summary>The whole selection, plus the forward change list and anything it had to disclose.</summary>
internal sealed record SelectionResult(
    IReadOnlyList<ProjectSelection> Projects,
    IReadOnlyList<ChangeEntry> Changes,
    IReadOnlyList<Notice> Notices)
{
    internal int SelectedCount => Projects.Sum(project => project.Selected.Count);

    /// <summary>
    /// A selection was made, nothing was selected, or there was nothing to analyse. The fourth
    /// outcome — failure — never reaches here.
    /// </summary>
    internal bool AnythingSelected =>
        Projects.Any(project => project.Mode != SelectionMode.Skip);
}
