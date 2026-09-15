using Reach.Graph;

namespace Reach.Tests.Graph;

/// <summary>
/// Turning a decoded type name into the generic definition the resolution index is keyed on.
/// </summary>
public class MetadataNameTests
{
    [Theory]
    [InlineData("System.Collections.Generic.List`1<System.Int32>", "System.Collections.Generic.List`1")]
    [InlineData("N.Outer`1+Inner<System.Int32>", "N.Outer`1+Inner")]
    [InlineData("N.Map`2<System.String,N.Box`1<System.Int32>>", "N.Map`2")]
    [InlineData("System.Int32", "System.Int32")]
    [InlineData("N.Outer+<M>d__0", "N.Outer+<M>d__0")]
    [InlineData("N.Outer+<>c__DisplayClass0_0", "N.Outer+<>c__DisplayClass0_0")]
    public void An_instantiation_is_dropped_and_nothing_else_is(string decoded, string expected) =>
        Assert.Equal(expected, MetadataNames.WithoutInstantiation(decoded));

    [Fact]
    public void A_compiler_generated_name_that_starts_with_an_angle_bracket_survives()
    {
        // `<>z__ReadOnlyArray`1` is what a collection expression compiles to. Cutting at the
        // first '<' leaves an empty type name, which resolves to nothing and silently loses
        // every call into it — found by running Reach on its own repository.
        Assert.Equal(
            "<>z__ReadOnlyArray`1",
            MetadataNames.WithoutInstantiation("<>z__ReadOnlyArray`1<System.Int32>"));

        Assert.Equal("<>z__ReadOnlyArray`1", MetadataNames.WithoutInstantiation("<>z__ReadOnlyArray`1"));
    }

    [Fact]
    public void An_unbalanced_name_is_left_alone() =>
        Assert.Equal("N.Broken<", MetadataNames.WithoutInstantiation("N.Broken<"));
}
