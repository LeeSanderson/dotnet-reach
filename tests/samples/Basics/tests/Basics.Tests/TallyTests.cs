using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class TallyTests
{
    [Fact]
    public void A_new_tally_counts_zero() => Assert.Equal(0, new Tally().Count);

    [Fact]
    public void Three_adds_count_three()
    {
        var tally = new Tally();

        tally.Add();
        tally.Add();
        tally.Add();

        Assert.Equal(3, tally.Count);
    }
}
