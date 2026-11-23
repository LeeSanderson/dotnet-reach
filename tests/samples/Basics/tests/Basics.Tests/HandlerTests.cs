using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class HandlerTests
{
    [Fact]
    public void A_foo_handler_describes_a_foo() => Assert.Equal("foo", new Handler<Foo>().Handle(new Foo()));

    [Fact]
    public void A_bar_handler_describes_a_bar() => Assert.Equal("bar", new Handler<Bar>().Handle(new Bar()));
}
