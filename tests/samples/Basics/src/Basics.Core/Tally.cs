namespace Basics.Core;

/// <summary>
/// Row 6's subject: its file moves to another directory, in the same namespace, which widens
/// the whole type. Only row 6's patch touches it.
/// </summary>
public sealed class Tally
{
    public int Count { get; private set; }

    public void Add() => Count++;
}
