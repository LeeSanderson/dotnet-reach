namespace Basics.Tests;

/// <summary>
/// Row 5's subject: temperatures the tests share. Row 5's patch deletes <see cref="BodyHeat"/>,
/// which no test calls.
/// </summary>
/// <remarks>
/// It lives in the test project on purpose. A member removed from <c>Basics.Core</c> would also
/// widen every project that references it (<c>recompilation-widening</c>). Nothing references
/// the test project, so only the deletion's own widening applies here: today that is the whole
/// assembly, which ticket 06 records as a finding, and by design it is this one type.
/// </remarks>
public static class Readings
{
    public static decimal Freezing() => 0m;

    public static decimal BodyHeat() => 37m;
}
