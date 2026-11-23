using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class InitialsTests
{
    [Fact]
    public void Initials_take_the_first_letter_of_each_name() => Assert.Equal("AL", Initials.Of("Ada Lovelace"));

    [Fact]
    public void A_single_name_gives_one_initial() => Assert.Equal("P", Initials.Of("Plato"));
}
