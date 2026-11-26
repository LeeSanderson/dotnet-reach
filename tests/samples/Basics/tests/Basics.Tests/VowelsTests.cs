using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class VowelsTests
{
    [Fact]
    public void Banana_has_three_vowels() => Assert.Equal(3, Vowels.In("Banana"));
}
