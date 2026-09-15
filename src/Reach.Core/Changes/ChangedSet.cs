using Reach.Projects;
using Reach.Reporting;

namespace Reach.Changes;

/// <summary>Which side of the comparison a member appeared on.</summary>
internal enum MemberChange
{
    Added,
    Modified,
    Removed,
}

/// <summary>
/// One member a change added, altered or removed. The declaring type is keyed on
/// fully-qualified name plus arity, never on file path, so a moved type is the same type.
/// </summary>
/// <param name="ChangesCompileTimeConstant">
/// A <c>const</c> value, an enum member value, or a declaration carrying a parameter default.
/// The compiler bakes these into consumers, so the declaring side alone cannot cover them —
/// recompilation widening reads this.
/// </param>
internal sealed record ChangedMember(
    string DeclaringType,
    MemberKey Member,
    MemberChange Change,
    string Path,
    bool ChangesCompileTimeConstant = false,
    DeclarationSpan Span = default)
{
    public override string ToString() => $"{DeclaringType}.{Member}";
}

/// <summary>
/// Where a declaration sits in its file, in the spelling debug symbols use: one-based lines
/// and columns. What the join matches on.
/// </summary>
internal readonly record struct DeclarationSpan(int StartLine, int StartColumn, int EndLine, int EndColumn)
{
    internal bool IsEmpty => StartLine == 0 && EndLine == 0;

    /// <summary>Whether a point sits inside this span, comparing line and column together.</summary>
    internal bool Contains(int line, int column) =>
        (line > StartLine || (line == StartLine && column >= StartColumn))
        && (line < EndLine || (line == EndLine && column <= EndColumn));
}

/// <summary>Why a widening was applied. Every one of these errs toward over-selection.</summary>
internal enum WideningReason
{
    /// <summary>
    /// A method was deleted. Unconditional, because a deletion can <em>rebind</em> rather than
    /// remove a binding: delete an <c>override bool Equals</c> and nothing referenced it by
    /// name, so compilation succeeds and every test asserting two equal values are equal now
    /// compares references.
    /// </summary>
    DeletedMember,

    /// <summary>A type's modifiers, attributes, base list or type parameters changed.</summary>
    TypeHeaderChanged,

    /// <summary>The type is declared in different files than it was at the baseline.</summary>
    TypeMoved,

    /// <summary>The type has no identity in the current binaries at all.</summary>
    DeletedType,
}

/// <summary>
/// Every surviving member of a type goes into the changed set, and the reverse walk runs as
/// normal. The same operation as whole-assembly widening, one granularity finer.
/// </summary>
internal sealed record TypeWidening(string DeclaringType, WideningReason Reason, string Path);

/// <summary>
/// Every method of an assembly goes into the changed set. The response to a change Reach can
/// attribute to an assembly but not to a method.
/// </summary>
/// <param name="Project">
/// The nearest ancestor project directory, which reproduces the SDK's default <c>**/*.cs</c>
/// globbing without running MSBuild. Null when no project contains the path at all, which is
/// the rule table's business.
/// </param>
internal sealed record AssemblyWidening(
    string DeclaringType,
    WideningReason Reason,
    string Path,
    ProjectFile? Project);

/// <summary>
/// The members a change added, altered or removed, plus everything that had to widen because
/// it could not be attributed to a member.
/// </summary>
/// <param name="UnmappablePaths">
/// Paths no member could be attributed to — a project file, a resource, a deleted source file
/// that declared no type. Routed by the tier ladder, which always widens.
/// </param>
internal sealed record ChangedSet(
    IReadOnlyList<ChangedMember> Members,
    IReadOnlyList<TypeWidening> TypeWidenings,
    IReadOnlyList<AssemblyWidening> AssemblyWidenings,
    IReadOnlyList<ChangedPath> Paths,
    IReadOnlyList<ChangedPath> UnmappablePaths,
    IReadOnlyList<Notice> Notices)
{
    internal static ChangedSet Empty { get; } = new([], [], [], [], [], []);

    /// <summary>Nothing changed at all — a different piece of news from an empty selection.</summary>
    internal bool IsEmpty =>
        Members.Count == 0
        && TypeWidenings.Count == 0
        && AssemblyWidenings.Count == 0
        && UnmappablePaths.Count == 0;

    /// <summary>Members that were removed. The one trigger recompilation widening cares about.</summary>
    internal IEnumerable<ChangedMember> RemovedMembers =>
        Members.Where(member => member.Change == MemberChange.Removed);
}
