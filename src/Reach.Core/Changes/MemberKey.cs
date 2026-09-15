namespace Reach.Changes;

/// <summary>What kind of declaration a member is. Distinguishes overload sets that share a name.</summary>
internal enum MemberKind
{
    Method,
    Constructor,
    Destructor,
    Property,
    Indexer,
    Field,
    Event,
    EnumMember,
    Operator,
    ConversionOperator,
    Delegate,
}

/// <summary>
/// Identifies one member <em>within its declaring type</em>, well enough to pair a baseline
/// declaration with a working-tree one.
/// </summary>
/// <remarks>
/// Parameter types are the spellings found in source — <c>int</c>, not <c>System.Int32</c> —
/// which is sound here and only here: both revisions are read by the same parser, so the two
/// sides agree by construction. Turning one of these into a method identity in the call graph
/// is the <em>join</em>, and the join is a separate step with a separate mechanism.
/// </remarks>
internal sealed record MemberKey(
    MemberKind Kind,
    string Name,
    int TypeParameterCount,
    IReadOnlyList<string> ParameterTypes)
{
    public bool Equals(MemberKey? other) =>
        other is not null
        && Kind == other.Kind
        && Name == other.Name
        && TypeParameterCount == other.TypeParameterCount
        && ParameterTypes.SequenceEqual(other.ParameterTypes, StringComparer.Ordinal);

    public override int GetHashCode() =>
        HashCode.Combine(Kind, Name, TypeParameterCount, ParameterTypes.Count);

    public override string ToString()
    {
        var generic = TypeParameterCount > 0 ? $"`{TypeParameterCount}" : string.Empty;

        return Kind switch
        {
            MemberKind.Field or MemberKind.EnumMember or MemberKind.Property or MemberKind.Event =>
                $"{Name}{generic}",
            _ => $"{Name}{generic}({string.Join(", ", ParameterTypes)})",
        };
    }
}
