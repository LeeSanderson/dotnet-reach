namespace Basics.Core;

/// <summary>
/// What <see cref="Handler{T}"/> handles, for row 24. Gap-shaped: no gate patch touches it.
/// </summary>
public abstract class Message
{
    public virtual string Describe() => "a message";
}

/// <summary>
/// Reached by a test only through <c>Handler&lt;Foo&gt;</c>. Row 24's patch changes it.
/// </summary>
public sealed class Foo : Message
{
    public override string Describe() => "foo";
}

/// <summary>
/// Reached by a test only through <c>Handler&lt;Bar&gt;</c>.
/// </summary>
public sealed class Bar : Message
{
    public override string Describe() => "bar";
}
