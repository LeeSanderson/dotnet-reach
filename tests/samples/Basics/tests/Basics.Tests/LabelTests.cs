using Basics.Core;
using Xunit;

namespace Basics.Tests;

public class LabelTests
{
    [Fact]
    public void An_upper_label_prints_in_capitals() => Assert.Equal("SALE", Printer.Print(new UpperLabel(), "sale"));

    [Fact]
    public void A_bracket_label_prints_in_brackets() => Assert.Equal("[sale]", Printer.Print(new BracketLabel(), "sale"));

    [Fact]
    public void An_upper_label_called_directly_renders_in_capitals() => Assert.Equal("NEW", new UpperLabel().Render("new"));
}
