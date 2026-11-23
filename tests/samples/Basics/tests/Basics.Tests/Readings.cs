namespace Basics.Tests;

/// <summary>
/// Row 5's subject: temperatures the tests share. Row 5's patch deletes <see cref="BodyHeat"/>,
/// which no test calls.
/// </summary>
/// <remarks>
/// It lives in the test project on purpose. Any member removed from <c>Basics.Core</c> also
/// widens every project that references it (<c>recompilation-widening</c>), which would select
/// every Basics test. Nothing references the test project, so here the deletion widens this one
/// type and nothing more.
/// </remarks>
public static class Readings
{
    public static decimal Freezing() => 0m;

    public static decimal BodyHeat() => 37m;
}
