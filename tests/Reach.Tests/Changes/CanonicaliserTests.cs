using Reach.Changes;

namespace Reach.Tests.Changes;

/// <summary>
/// The declaration-level comparison, in memory. Every case here is a property the changed set
/// depends on, and each is cheap enough that there is no reason to prove it through git.
/// </summary>
public class CanonicaliserTests
{
    private static string Declaration(string source, string type = "C", string member = "M")
    {
        var declared = SourceRevision.DeclaredTypes(source);
        var found = declared[type];

        return found.Members.Single(entry => entry.Key.Name == member).Value.Declaration;
    }

    private static string Header(string source, string type = "C") =>
        SourceRevision.DeclaredTypes(source)[type].Header;

    private static void AssertSame(string before, string after, string type = "C", string member = "M") =>
        Assert.Equal(Declaration(before, type, member), Declaration(after, type, member));

    private static void AssertDifferent(string before, string after, string type = "C", string member = "M") =>
        Assert.NotEqual(Declaration(before, type, member), Declaration(after, type, member));

    // ---- Trivia is discarded --------------------------------------------------------------

    [Fact]
    public void A_comment_only_change_does_not_change_a_declaration()
    {
        // Over a declaration carrying attributes and an initializer, not just a body: the
        // hash covers the whole declaration now, so trivia-stripping has more to get right.
        AssertSame(
            """
            class C
            {
                [Obsolete("why")]
                public int M(int x = 3) => x + 1;
            }
            """,
            """
            class C
            {
                // a new comment
                /// <summary>And a doc comment.</summary>
                [Obsolete("why")] // trailing
                public int M(int x = 3) => x + 1; /* and another */
            }
            """);
    }

    [Fact]
    public void A_whitespace_only_reformat_does_not_change_a_declaration()
    {
        AssertSame(
            "class C { public int M(int x) { return x; } }",
            """
            class C
            {
                public int M(
                    int x)
                {
                        return
                            x;
                }
            }
            """);
    }

    [Fact]
    public void A_region_or_a_pragma_does_not_change_a_declaration()
    {
        AssertSame(
            """
            class C
            {
                public int M() => 1;
            }
            """,
            """
            class C
            {
                #region The maths
                #pragma warning disable CS1591
                public int M() => 1;
                #pragma warning restore CS1591
                #endregion
            }
            """);
    }

    // ---- The declaration, not the body -----------------------------------------------------

    [Fact]
    public void Adding_Fact_to_an_existing_method_is_a_change()
    {
        // The cheapest test of the most expensive regression in the set: it is what makes
        // "new since the baseline" derivable from the changed set with no separate mechanism.
        AssertDifferent(
            "class C { public void M() { } }",
            "class C { [Fact] public void M() { } }");
    }

    [Fact]
    public void A_changed_attribute_argument_is_a_change() =>
        AssertDifferent(
            """class C { [Obsolete("old")] public void M() { } }""",
            """class C { [Obsolete("new")] public void M() { } }""");

    [Fact]
    public void A_changed_const_value_is_a_change() =>
        AssertDifferent(
            "class C { public const int M = 1; }",
            "class C { public const int M = 2; }");

    [Fact]
    public void A_changed_field_initializer_is_a_change() =>
        AssertDifferent(
            "class C { public int M = 1; }",
            "class C { public int M = 2; }");

    [Fact]
    public void A_changed_enum_member_value_is_a_change() =>
        AssertDifferent(
            "enum C { M = 1 }",
            "enum C { M = 2 }");

    [Fact]
    public void A_changed_default_parameter_value_is_a_change() =>
        AssertDifferent(
            "class C { public void M(int x = 1) { } }",
            "class C { public void M(int x = 2) { } }");

    [Fact]
    public void A_changed_modifier_is_a_change() =>
        AssertDifferent(
            "class C { void M() { } }",
            "class C { public virtual void M() { } }");

    [Fact]
    public void A_changed_body_is_a_change() =>
        AssertDifferent(
            "class C { public int M() => 1; }",
            "class C { public int M() => 2; }");

    // ---- Conditional compilation ------------------------------------------------------------

    [Fact]
    public void A_change_inside_an_inactive_if_block_is_a_change()
    {
        // The branch the parser did not take is trivia, so the token stream is identical
        // either way. Discarding it would lose a change that another configuration compiles.
        AssertDifferent(
            """
            class C
            {
                public int M()
                {
            #if DEBUG
                    return 1;
            #else
                    return 2;
            #endif
                }
            }
            """,
            """
            class C
            {
                public int M()
                {
            #if DEBUG
                    return 1;
            #else
                    return 3;
            #endif
                }
            }
            """);
    }

    // ---- Type headers -----------------------------------------------------------------------

    [Fact]
    public void A_type_attribute_changes_the_header_and_no_member()
    {
        var before = "class C { public void M() { } }";
        var after = "[Collection(\"db\")] class C { public void M() { } }";

        // A test class gaining an attribute that makes its methods discoverable is invisible
        // to any member-level comparison, which is why the header is its own unit.
        Assert.NotEqual(Header(before), Header(after));
        AssertSame(before, after);
    }

    [Fact]
    public void A_changed_base_list_changes_the_header() =>
        Assert.NotEqual(
            Header("class C { }"),
            Header("class C : IDisposable { }"));

    [Fact]
    public void A_member_body_does_not_change_the_header() =>
        Assert.Equal(
            Header("class C { public int M() => 1; }"),
            Header("class C { public int M() => 2; }"));

    // ---- Names and keys -----------------------------------------------------------------------

    [Fact]
    public void A_type_is_keyed_on_its_fully_qualified_name_plus_arity()
    {
        var declared = SourceRevision.DeclaredTypes(
            """
            namespace Contoso.Billing;

            public class Invoice
            {
                public class Line { }
            }

            public class Ledger<TAccount, TEntry> { }
            """);

        Assert.Equal(
            ["Contoso.Billing.Invoice", "Contoso.Billing.Invoice+Line", "Contoso.Billing.Ledger`2"],
            declared.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Overloads_are_separate_members()
    {
        var declared = SourceRevision.DeclaredTypes(
            "class C { void M(int x) { } void M(string x) { } void M<T>(T x) { } }");

        Assert.Equal(3, declared["C"].Members.Count);
    }

    [Fact]
    public void Several_fields_in_one_declaration_are_separate_members()
    {
        var declared = SourceRevision.DeclaredTypes("class C { int a = 1, b = 2; }");

        Assert.Equal(["a", "b"], declared["C"].Members.Keys.Select(key => key.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void One_field_changing_leaves_the_other_alone()
    {
        var before = SourceRevision.DeclaredTypes("class C { int a = 1, b = 2; }")["C"].Members;
        var after = SourceRevision.DeclaredTypes("class C { int a = 1, b = 3; }")["C"].Members;

        var a = new MemberKey(MemberKind.Field, "a", 0, []);
        var b = new MemberKey(MemberKind.Field, "b", 0, []);

        Assert.Equal(before[a].Declaration, after[a].Declaration);
        Assert.NotEqual(before[b].Declaration, after[b].Declaration);
    }

    [Fact]
    public void A_partial_type_declared_twice_in_one_file_is_one_type()
    {
        var declared = SourceRevision.DeclaredTypes(
            "partial class C { void A() { } } partial class C { void B() { } }");

        Assert.Equal(2, Assert.Single(declared).Value.Members.Count);
    }

    [Fact]
    public void A_nested_type_is_not_a_member_of_its_container()
    {
        var declared = SourceRevision.DeclaredTypes("class C { class Inner { } void M() { } }");

        Assert.Equal(["M"], declared["C"].Members.Keys.Select(key => key.Name));
    }

    [Theory]
    [InlineData("const int M = 1;", true)]
    [InlineData("int M = 1;", false)]
    [InlineData("void M(int x = 1) { }", true)]
    [InlineData("void M(int x) { }", false)]
    public void Compile_time_constants_are_flagged_for_recompilation_widening(string member, bool expected)
    {
        var declared = SourceRevision.DeclaredTypes($"class C {{ {member} }}");

        Assert.Equal(expected, declared["C"].Members.Values.Single().IsCompileTimeConstant);
    }

    [Fact]
    public void An_enum_member_is_always_a_compile_time_constant()
    {
        var declared = SourceRevision.DeclaredTypes("enum C { M }");

        Assert.True(declared["C"].Members.Values.Single().IsCompileTimeConstant);
    }
}
