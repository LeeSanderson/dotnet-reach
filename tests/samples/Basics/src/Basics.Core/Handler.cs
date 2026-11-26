namespace Basics.Core;

/// <summary>
/// Row 24's subject, a known gap: one generic definition used as <c>Handler&lt;Foo&gt;</c> and
/// <c>Handler&lt;Bar&gt;</c>. Reach keeps one node for the definition, so code reached only
/// through <c>Handler&lt;Foo&gt;</c> also selects the users of <c>Handler&lt;Bar&gt;</c>.
/// Gap-shaped: no gate patch touches it.
/// </summary>
public sealed class Handler<T>
    where T : Message
{
    public string Handle(T message) => message.Describe();
}
