using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Reach.Assemblies;
using Reach.Changes;
using Reach.Graph;
using Reach.Join;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Join;

/// <summary>
/// The join, over real IL and real debug symbols. Every hard shape the spike measured has a
/// named test here, because the shapes are exactly where a join silently matches the wrong
/// method.
/// </summary>
public class SpanJoinTests
{
    private const string DocumentPath = "/repo/src/Subject.cs";

    /// <summary>
    /// Compiles <paramref name="source"/>, joins the declaration named by
    /// <paramref name="declaringType"/> and <paramref name="member"/>, and returns the IL
    /// method names it reached.
    /// </summary>
    private static IReadOnlyList<string> Join(string source, string declaringType, string member)
    {
        using var subject = Subject.Compile(source);

        var declaration = Declaration(source, declaringType, member);
        var result = new SpanJoin(subject.Assemblies, "/repo").Resolve(declaration);

        return [.. result.Methods.Select(subject.NameOf).Order(StringComparer.Ordinal)];
    }

    private static ChangedMember Declaration(string source, string declaringType, string member)
    {
        var declared = SourceRevision.DeclaredTypes(source);

        Assert.True(declared.ContainsKey(declaringType), $"No type {declaringType} in the fixture.");

        var found = declared[declaringType].Members
            .Where(entry => entry.Key.ToString().StartsWith(member, StringComparison.Ordinal))
            .ToArray();

        Assert.True(found.Length == 1, $"Expected one member matching '{member}', found {found.Length}.");

        return new ChangedMember(
            declaringType,
            found[0].Key,
            MemberChange.Modified,
            "src/Subject.cs",
            found[0].Value.IsCompileTimeConstant,
            found[0].Value.Span);
    }

    // ---- Overloads and generics ---------------------------------------------------------------

    private const string Overloads = """
        namespace N;

        public class Subject
        {
            public int Overload(int x) => x;
            public int Overload(long x) => (int)x + 1;
            public int Overload(ref int x) => x + 2;
        }
        """;

    [Theory]
    [InlineData("Overload(int")]
    [InlineData("Overload(long")]
    // C# forbids overloading on out/in against ref, so ref is the one that can sit beside a
    // by-value overload — and it is the pair that shares a MemberKey unless the passing mode
    // is part of it.
    [InlineData("Overload(ref int")]
    public void Each_overload_joins_to_exactly_one_method(string member)
    {
        // Positional, so ref needs no mapping at all — and a wrong answer here would be
        // under-selection, which is the failure mode the mechanism was chosen to avoid.
        Assert.Equal(["Subject::Overload"], Join(Overloads, "N.Subject", member));
    }

    [Fact]
    public void Two_overloads_join_to_two_different_methods()
    {
        using var subject = Subject.Compile(Overloads);
        var join = new SpanJoin(subject.Assemblies, "/repo");

        var first = join.Resolve(Declaration(Overloads, "N.Subject", "Overload(int"));
        var second = join.Resolve(Declaration(Overloads, "N.Subject", "Overload(long"));

        Assert.NotEqual(Assert.Single(first.Methods), Assert.Single(second.Methods));
    }

    [Fact]
    public void A_generic_method_joins_to_its_definition() =>
        Assert.Equal(
            ["Subject::Generic"],
            Join(
                """
                namespace N;

                public class Subject
                {
                    public T Generic<T>(T value) => value;
                    public T Generic<T, U>(T value, U other) => value;
                }
                """,
                "N.Subject",
                "Generic`1"));

    [Fact]
    public void A_member_of_a_nested_generic_type_joins() =>
        Assert.Equal(
            ["Outer+Inner`1::Method"],
            Join(
                """
                namespace N;

                public class Outer
                {
                    public class Inner<T>
                    {
                        public int Method() => 1;
                    }
                }
                """,
                "N.Outer+Inner`1",
                "Method"));

    // ---- The fan-out the join owes -----------------------------------------------------------

    [Fact]
    public void A_property_joins_to_its_accessors_and_not_to_a_property_node() =>
        Assert.Equal(
            ["Subject::get_Total", "Subject::set_Total"],
            Join(
                """
                namespace N;

                public class Subject
                {
                    private int total;
                    public int Total { get => total; set => total = value; }
                }
                """,
                "N.Subject",
                "Total"));

    [Fact]
    public void An_auto_property_with_an_initializer_joins_to_the_constructor_too()
    {
        var joined = Join(
            """
            namespace N;

            public class Subject
            {
                public int Total { get; set; } = 7;
            }
            """,
            "N.Subject",
            "Total");

        // The initializer's IL lands in the constructor, and the constructor's sequence point
        // for it sits inside the property declaration. No rule of its own is needed.
        Assert.Contains("Subject::.ctor", joined);
        Assert.Contains("Subject::get_Total", joined);
    }

    [Fact]
    public void An_indexer_joins_to_get_Item() =>
        Assert.Equal(
            ["Subject::get_Item"],
            Join(
                "namespace N;\n\npublic class Subject\n{\n    public int this[int index] => index;\n}",
                "N.Subject",
                "this[]"));

    [Fact]
    public void An_operator_joins_to_its_IL_name() =>
        Assert.Equal(
            ["Subject::op_Equality"],
            Join(
                """
                namespace N;

                public class Subject
                {
                    public static bool operator ==(Subject a, Subject b) => true;
                    public static bool operator !=(Subject a, Subject b) => false;
                    public override bool Equals(object? o) => true;
                    public override int GetHashCode() => 0;
                }
                """,
                "N.Subject",
                "op=="));

    [Fact]
    public void An_explicit_interface_implementation_joins() =>
        Assert.Equal(
            ["Subject::N.IShape.Draw"],
            Join(
                """
                namespace N;

                public interface IShape { void Draw(); }

                public class Subject : IShape
                {
                    void IShape.Draw() { }
                }
                """,
                "N.Subject",
                "Draw"));

    [Fact]
    public void A_method_containing_a_local_function_joins_to_both()
    {
        var joined = Join(
            """
            namespace N;

            public class Subject
            {
                public int Outer()
                {
                    return Helper();
                    int Helper() => 1;
                }
            }
            """,
            "N.Subject",
            "Outer");

        // The generated member sits inside the kernel method's span, so containment on the
        // changed side falls out of being positional.
        Assert.Contains("Subject::Outer", joined);
        Assert.Contains(joined, name => name.Contains("Helper", StringComparison.Ordinal));
    }

    [Fact]
    public void A_method_containing_a_lambda_joins_to_both()
    {
        var joined = Join(
            """
            namespace N;

            public class Subject
            {
                public System.Func<int> Make()
                {
                    var seed = 1;
                    return () => seed + 1;
                }
            }
            """,
            "N.Subject",
            "Make");

        Assert.Contains("Subject::Make", joined);
        Assert.Contains(joined, name => name.Contains("b__", StringComparison.Ordinal));
    }

    [Fact]
    public void An_async_method_joins_to_its_state_machine()
    {
        var joined = Join(
            """
            namespace N;

            public class Subject
            {
                public async System.Threading.Tasks.Task<int> Work()
                {
                    await System.Threading.Tasks.Task.Yield();
                    return 1;
                }
            }
            """,
            "N.Subject",
            "Work");

        // An async kernel keeps no sequence points of its own — its body is state-machine
        // setup, marked hidden — so the join reaches MoveNext and *not* Work. That is the
        // right answer: MoveNext is where the changed code went, and the containment edge
        // from the kernel to it is what lets the reverse walk get back out of the BCL.
        Assert.Contains(joined, name => name.Contains("MoveNext", StringComparison.Ordinal));
        Assert.Contains(joined, name => name.Contains("<Work>d__", StringComparison.Ordinal));
    }

    [Fact]
    public void A_file_scoped_type_joins_despite_its_mangled_IL_name()
    {
        var joined = Join(
            """
            namespace N;

            file class Hidden
            {
                public int Method() => 1;
            }
            """,
            "N.Hidden",
            "Method");

        // The IL name carries a hash of the file path, which no name derived from source can
        // reconstruct. This one shape is why the signature candidate could never have won.
        Assert.Contains("Hidden::Method", Assert.Single(joined));
    }

    // ---- Falling through ------------------------------------------------------------------------

    [Theory]
    [InlineData("N.Shapes", "Draw", "public interface Shapes { void Draw(); }")]
    [InlineData("N.Subject", "Abstract", "public abstract class Subject { public abstract void Abstract(); }")]
    [InlineData("N.Subject", "External", "public class Subject { public extern int External(); }")]
    public void A_member_with_no_IL_does_not_join(string declaringType, string member, string body)
    {
        using var subject = Subject.Compile("namespace N;\n\n" + body);

        var result = new SpanJoin(subject.Assemblies, "/repo")
            .Resolve(Declaration("namespace N;\n\n" + body, declaringType, member));

        // Which is what falls through to whole-assembly widening for its project —
        // over-selection, and what makes a phantom member from a misparse survivable rather
        // than a silent hole.
        Assert.False(result.Joined);
        Assert.Empty(result.Methods);
    }

    [Fact]
    public void A_declaration_in_a_file_the_symbols_never_saw_does_not_join()
    {
        const string Source = "namespace N; public class Subject { public int M() => 1; }";

        using var subject = Subject.Compile(Source);

        var declaration = Declaration(Source, "N.Subject", "M") with { Path = "src/Never.cs" };
        var result = new SpanJoin(subject.Assemblies, "/repo").Resolve(declaration);

        Assert.False(result.Joined);
    }

    [Fact]
    public void A_declaration_with_no_span_does_not_join()
    {
        const string Source = "namespace N; public class Subject { public int M() => 1; }";

        using var subject = Subject.Compile(Source);

        var declaration = Declaration(Source, "N.Subject", "M") with { Span = default };

        Assert.False(new SpanJoin(subject.Assemblies, "/repo").Resolve(declaration).Joined);
    }

    // ---- Multi-targeting ------------------------------------------------------------------------

    [Fact]
    public void One_declaration_joins_into_every_assembly_instance_compiled_from_it()
    {
        const string Source = "namespace N; public class Subject { public int M() => 1; }";

        using var subject = Subject.Compile(Source, instances: 2);

        var result = new SpanJoin(subject.Assemblies, "/repo").Resolve(Declaration(Source, "N.Subject", "M"));

        // A multi-targeted project contributes several assembly instances, and a change to the
        // source changes all of them.
        Assert.Equal(2, result.Methods.Count);
        Assert.Equal(2, result.Methods.Select(method => method.Ordinal).Distinct().Count());
    }

    /// <summary>The fixture compiled once, with a way to read back a method's name.</summary>
    private sealed class Subject : IDisposable
    {
        private readonly List<IDisposable> open = [];

        private Subject(IReadOnlyList<GraphAssembly> assemblies) => Assemblies = assemblies;

        internal IReadOnlyList<GraphAssembly> Assemblies { get; private set; }

        internal static Subject Compile(string source, int instances = 1)
        {
            var subject = new Subject([]);
            var assemblies = new List<GraphAssembly>();

            for (var index = 0; index < instances; index++)
            {
                var compiled = Compiled.Assembly("Subject", source, DocumentPath);

                var reader = new PEReader(ImmutableArray.Create(compiled.Image));
                var symbols = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(compiled.Symbols));

                subject.open.Add(reader);
                subject.open.Add(symbols);

                assemblies.Add(new GraphAssembly(
                    index,
                    "Subject",
                    TargetFrameworkMoniker.Parse("net10.0"),
                    reader.GetMetadataReader(),
                    reader,
                    symbols.GetMetadataReader()));
            }

            subject.Assemblies = assemblies;

            return subject;
        }

        internal string NameOf(MethodId method)
        {
            var reader = Assemblies[method.Ordinal].Reader;

            foreach (var handle in reader.MethodDefinitions)
            {
                if (MetadataTokens.GetToken(handle) != method.Token)
                {
                    continue;
                }

                var definition = reader.GetMethodDefinition(handle);
                var declaringType = MetadataNames.FullNameOf(reader, definition.GetDeclaringType());

                // The namespace goes, the nesting and the compiler mangling stay: a test that
                // cannot see <Work>d__0 cannot assert anything useful about it.
                return declaringType.Split('.')[^1] + "::" + reader.GetString(definition.Name);
            }

            return method.ToString();
        }

        public void Dispose()
        {
            foreach (var item in open)
            {
                item.Dispose();
            }
        }
    }
}
