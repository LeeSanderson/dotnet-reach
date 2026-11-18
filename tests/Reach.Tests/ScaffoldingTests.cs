using System.Reflection;
using System.Runtime.CompilerServices;

namespace Reach.Tests;

/// <summary>
/// The scaffolding's own acceptance criteria: the test project can reach Reach.Core's
/// internals, and Reach.Cli cannot. Cli uses Core through its public types.
/// </summary>
public class ScaffoldingTests
{
    [Fact]
    public void Tests_can_reach_Core_internals()
    {
        Assert.False(string.IsNullOrEmpty(Product.Version));
    }

    [Fact]
    public void Cli_is_not_a_friend_of_Core()
    {
        var friends = typeof(Product).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName);

        Assert.DoesNotContain("Reach.Cli", friends);
    }
}
