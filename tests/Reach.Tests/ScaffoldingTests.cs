namespace Reach.Tests;

/// <summary>
/// The scaffolding's own acceptance criteria: the test project can reach Reach.Core's
/// internals, and nothing in Reach.Core is public.
/// </summary>
public class ScaffoldingTests
{
    [Fact]
    public void Tests_can_reach_Core_internals()
    {
        Assert.False(string.IsNullOrEmpty(Product.Version));
    }

    [Fact]
    public void Nothing_in_Core_is_public()
    {
        var exported = typeof(Product).Assembly.GetExportedTypes();

        Assert.Empty(exported);
    }
}
