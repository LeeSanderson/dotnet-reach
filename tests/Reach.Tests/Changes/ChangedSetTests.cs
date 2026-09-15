using Reach.Changes;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Changes;

/// <summary>
/// The changed set against real git history. What only a repository can establish: that both
/// revisions are read, that a move arrives as a delete plus an add, and that untracked files
/// are in and ignored ones are out.
/// </summary>
public class ChangedSetTests
{
    private const string Widget =
        """
        namespace Contoso;

        public class Widget
        {
            public int Spin() => 1;
            public int Wobble() => 2;
        }
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string[] Names(IEnumerable<ChangedMember> members) =>
        [.. members.Select(member => member.ToString()).Order(StringComparer.Ordinal)];

    // ---- The negative assertions -------------------------------------------------------------

    [Fact]
    public async Task A_comment_only_change_selects_nothing()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write(
            "src/Core/Widget.cs",
            """
            namespace Contoso;

            /// <summary>A widget.</summary>
            public class Widget
            {
                // spins
                public int Spin() => 1;
                public int Wobble() => 2; // wobbles
            }
            """);

        var changes = await repository.ChangesAsync(baseline, Token);

        Assert.True(changes.IsEmpty);
        Assert.Empty(changes.Members);
        Assert.Empty(changes.TypeWidenings);

        // The path is still reported as changed — git said so, and the report says what it saw.
        Assert.Single(changes.Paths);
    }

    [Fact]
    public async Task A_whitespace_only_reformat_selects_nothing()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write(
            "src/Core/Widget.cs",
            "namespace Contoso;\npublic class Widget{public int Spin()=>1;public int Wobble()=>2;}");

        var changes = await repository.ChangesAsync(baseline, Token);

        Assert.True(changes.IsEmpty);
    }

    // ---- Members ------------------------------------------------------------------------------

    [Fact]
    public async Task A_changed_method_body_is_one_changed_member()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/Widget.cs", Widget.Replace("Spin() => 1", "Spin() => 42"));

        var changes = await repository.ChangesAsync(baseline, Token);

        Assert.Equal(["Contoso.Widget.Spin()"], Names(changes.Members));
        Assert.Equal(MemberChange.Modified, Assert.Single(changes.Members).Change);
        Assert.Empty(changes.TypeWidenings);
    }

    [Fact]
    public async Task Fact_added_to_an_existing_method_is_a_changed_member()
    {
        using var repository = ChangeRepository.Create();

        repository.Write(
            "tests/Tests/WidgetTests.cs",
            """
            namespace Contoso.Tests;

            public class WidgetTests
            {
                public void Spins() { }
            }
            """);

        var baseline = repository.CommitBaseline();

        repository.Write(
            "tests/Tests/WidgetTests.cs",
            """
            namespace Contoso.Tests;

            public class WidgetTests
            {
                [Fact]
                public void Spins() { }
            }
            """);

        var changes = await repository.ChangesAsync(baseline, Token);

        // "New since the baseline" stays derivable from the changed set, and needs no
        // separate mechanism.
        Assert.Equal(["Contoso.Tests.WidgetTests.Spins()"], Names(changes.Members));
    }

    [Fact]
    public async Task An_added_type_makes_every_member_a_root()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write(
            "src/Core/Gadget.cs",
            """
            namespace Contoso;

            public class Gadget
            {
                public int A() => 1;
                public int B() => 2;
            }
            """);

        var changes = await repository.ChangesAsync(baseline, Token);

        Assert.Equal(["Contoso.Gadget.A()", "Contoso.Gadget.B()"], Names(changes.Members));
        Assert.All(changes.Members, member => Assert.Equal(MemberChange.Added, member.Change));
    }

    // ---- Deletions -------------------------------------------------------------------------------

    [Fact]
    public async Task A_deleted_method_widens_its_declaring_type()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write(
            "src/Core/Widget.cs",
            """
            namespace Contoso;

            public class Widget
            {
                public int Spin() => 1;
            }
            """);

        var changes = await repository.ChangesAsync(baseline, Token);

        // Unconditional: a deletion can rebind rather than remove a binding, and no clause
        // separating the inert deletions from the dangerous ones is safe to write.
        var widening = Assert.Single(changes.TypeWidenings);
        Assert.Equal("Contoso.Widget", widening.DeclaringType);
        Assert.Equal(WideningReason.DeletedMember, widening.Reason);

        // And the removal is recorded, because it is one of recompilation widening's triggers.
        Assert.Equal(["Contoso.Widget.Wobble()"], Names(changes.RemovedMembers));
    }

    [Fact]
    public async Task A_deleted_type_whose_file_survives_widens_the_assembly()
    {
        using var repository = ChangeRepository.Create();

        repository.Write(
            "src/Core/Widget.cs",
            Widget + "\n\npublic class Doomed { public int X() => 1; }");

        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/Widget.cs", Widget);

        var changes = await repository.ChangesAsync(baseline, Token);

        var widening = Assert.Single(changes.AssemblyWidenings);
        Assert.Equal("Contoso.Doomed", widening.DeclaringType);
        Assert.Equal(WideningReason.DeletedType, widening.Reason);

        // Named by directory containment — the nearest ancestor project directory, which
        // reproduces the SDK's default **/*.cs globbing without running MSBuild.
        Assert.Equal("Core", widening.Project?.Name);
    }

    [Fact]
    public async Task A_deleted_file_routes_through_the_types_it_declared_at_the_baseline()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Delete("src/Core/Widget.cs");

        var changes = await repository.ChangesAsync(baseline, Token);

        // Reading the file at the baseline revision is the only way to know what it declared.
        var widening = Assert.Single(changes.AssemblyWidenings);
        Assert.Equal("Contoso.Widget", widening.DeclaringType);
        Assert.Equal("Core", widening.Project?.Name);
    }

    [Fact]
    public async Task A_deleted_file_declaring_no_type_goes_to_the_tier_ladder()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Usings.cs", "global using System;\nglobal using System.Linq;\n");
        var baseline = repository.CommitBaseline();

        repository.Delete("src/Core/Usings.cs");

        var changes = await repository.ChangesAsync(baseline, Token);

        // Nothing to route through a type, so the path is handed to the rule table rather
        // than quietly dropped.
        Assert.Equal(["src/Core/Usings.cs"], changes.UnmappablePaths.Select(path => path.Path));
    }

    // ---- Moves -------------------------------------------------------------------------------------

    [Fact]
    public async Task A_type_moved_between_directories_yields_whole_type_widening()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Move("src/Core/Widget.cs", "src/Core/Parts/Widget.cs");

        var changes = await repository.ChangesAsync(baseline, Token);

        // Found on both sides, because --no-renames turns a move into a delete plus an add
        // and the type is keyed on its name. The file it lives in decides which project
        // compiles it, so the move alone is worth widening.
        var widening = Assert.Single(changes.TypeWidenings);
        Assert.Equal("Contoso.Widget", widening.DeclaringType);
        Assert.Equal(WideningReason.TypeMoved, widening.Reason);
        Assert.Empty(changes.AssemblyWidenings);
        Assert.Empty(changes.Members);
    }

    [Fact]
    public async Task A_type_moved_between_namespaces_yields_whole_assembly_widening()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/Widget.cs", Widget.Replace("namespace Contoso;", "namespace Contoso.Parts;"));

        var changes = await repository.ChangesAsync(baseline, Token);

        // The asymmetry with a directory move is deliberate: a namespace is part of the
        // metadata name, so the move changes what runtime binding sees — serialization
        // discriminators, convention-based registration, Type.GetType — and those are blind
        // spots Reach cannot see into.
        var widening = Assert.Single(changes.AssemblyWidenings);
        Assert.Equal("Contoso.Widget", widening.DeclaringType);
        Assert.Equal(WideningReason.DeletedType, widening.Reason);
    }

    // ---- Type headers ---------------------------------------------------------------------------------

    [Fact]
    public async Task A_type_header_change_yields_whole_type_widening()
    {
        using var repository = ChangeRepository.Create();

        repository.Write(
            "tests/Tests/WidgetTests.cs",
            """
            namespace Contoso.Tests;

            public class WidgetTests
            {
                [Fact] public void Spins() { }
            }
            """);

        var baseline = repository.CommitBaseline();

        repository.Write(
            "tests/Tests/WidgetTests.cs",
            """
            namespace Contoso.Tests;

            [Collection("database")]
            public class WidgetTests
            {
                [Fact] public void Spins() { }
            }
            """);

        var changes = await repository.ChangesAsync(baseline, Token);

        var widening = Assert.Single(changes.TypeWidenings);
        Assert.Equal(WideningReason.TypeHeaderChanged, widening.Reason);
        Assert.Empty(changes.Members);
    }

    // ---- Sources ----------------------------------------------------------------------------------------

    [Fact]
    public async Task The_changed_set_spans_committed_staged_and_unstaged()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/A.cs", "namespace N; public class A { public int M() => 1; }");
        repository.Write("src/Core/B.cs", "namespace N; public class B { public int M() => 1; }");
        repository.Write("src/Core/C.cs", "namespace N; public class C { public int M() => 1; }");
        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/A.cs", "namespace N; public class A { public int M() => 2; }");
        repository.Commit("committed since the baseline");

        repository.Write("src/Core/B.cs", "namespace N; public class B { public int M() => 2; }");
        repository.Git("add", "src/Core/B.cs");

        repository.Write("src/Core/C.cs", "namespace N; public class C { public int M() => 2; }");

        var changes = await repository.ChangesAsync(baseline, Token);

        Assert.Equal(["N.A.M()", "N.B.M()", "N.C.M()"], Names(changes.Members));
    }

    [Fact]
    public async Task Untracked_files_are_in_the_changed_set_and_ignored_ones_are_not()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/New.cs", "namespace N; public class New { public int M() => 1; }");
        repository.Write("src/Core/obj/Generated.cs", "namespace N; public class Generated { public int M() => 1; }");

        var changes = await repository.ChangesAsync(baseline, Token);

        // Without --exclude-standard every generated .cs under obj/ returns as untracked and
        // every project's assembly widens on every run.
        Assert.Equal(["N.New.M()"], Names(changes.Members));
        Assert.DoesNotContain(changes.Paths, path => path.Path.Contains("obj/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_untracked_source_file_is_disclosed()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write("src/Core/New.cs", "namespace N; public class New { }");

        var changes = await repository.ChangesAsync(baseline, Token);

        // Either intentional code generation or a forgotten `git add`, and both bear on
        // trusting a surprising selection.
        var notice = Assert.Single(changes.Notices);
        Assert.Equal(NoticeCodes.UntrackedSourceInChangeSet, notice.Code);
        Assert.Contains("src/Core/New.cs", notice.Message);
    }

    [Fact]
    public async Task A_non_source_change_goes_to_the_tier_ladder()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        repository.Write(
            "src/Core/Core.csproj",
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><PackageReference Include="Newtonsoft.Json" Version="13.0.3" /></ItemGroup>
            </Project>
            """);

        var changes = await repository.ChangesAsync(baseline, Token);

        Assert.Equal(["src/Core/Core.csproj"], changes.UnmappablePaths.Select(path => path.Path));
        Assert.Empty(changes.Members);
    }

    [Fact]
    public async Task Nothing_changed_is_an_empty_changed_set()
    {
        using var repository = ChangeRepository.Create();

        repository.Write("src/Core/Widget.cs", Widget);
        var baseline = repository.CommitBaseline();

        var changes = await repository.ChangesAsync(baseline, Token);

        Assert.True(changes.IsEmpty);
        Assert.Empty(changes.Paths);
    }
}
