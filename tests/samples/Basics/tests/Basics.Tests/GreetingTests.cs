using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class GreetingTests
{
    [Fact]
    public void Hello_names_the_person() => Assert.Equal("Hello, Ada", Greeting.Hello("Ada"));

    [Fact]
    public void Goodbye_names_the_person() => Assert.Equal("Goodbye, Ada", Greeting.Goodbye("Ada"));
}
