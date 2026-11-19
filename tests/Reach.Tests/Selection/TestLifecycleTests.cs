using Reach.Graph;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Selection;

/// <summary>
/// A test framework runs more of a test class than the test method: its constructor, its
/// disposal, and whatever setup and teardown it declares. No instruction in the test method
/// calls any of them, so without a synthesised edge a change reached only from there selects
/// nothing — and fails silently.
/// </summary>
public class TestLifecycleTests
{
    private const string Core = """
        namespace N;

        public class Widget
        {
            public static int Prepare() => 1;
            public static int Spin() => 2;
        }
        """;

    // ---- xUnit -----------------------------------------------------------------------------------

    [Fact]
    public void A_change_reached_only_from_the_constructor_selects_every_test_in_the_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                private readonly int _seed;

                public WidgetTests() => _seed = N.Widget.Prepare();

                [Xunit.Fact]
                public void First() { }

                [Xunit.Fact]
                public void Second() { }
            }

            public class OtherTests
            {
                [Xunit.Fact]
                public void Elsewhere() { }
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First", "Second"], selections.SelectedNames);
        Assert.All(selections.TestProject.Selected, test => Assert.Equal(PathClass.Compiled, test.Class));
    }

    [Fact]
    public void A_change_reached_only_from_a_field_initialiser_selects_the_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                private readonly int _seed = N.Widget.Prepare();

                [Xunit.Fact]
                public void First() { }
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_a_static_initialiser_selects_the_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                private static readonly int Seed = N.Widget.Prepare();

                [Xunit.Fact]
                public void First() { }
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_Dispose_selects_the_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public sealed class WidgetTests : System.IDisposable
            {
                [Xunit.Fact]
                public void First() { }

                public void Dispose() => N.Widget.Prepare();
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_an_explicit_DisposeAsync_selects_the_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public sealed class WidgetTests : System.IAsyncDisposable
            {
                [Xunit.Fact]
                public void First() { }

                System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync()
                {
                    N.Widget.Prepare();
                    return default;
                }
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_IAsyncLifetime_selects_the_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public sealed class WidgetTests : Xunit.IAsyncLifetime
            {
                [Xunit.Fact]
                public void First() { }

                public async System.Threading.Tasks.ValueTask InitializeAsync()
                {
                    await System.Threading.Tasks.Task.Yield();
                    N.Widget.Prepare();
                }

                public System.Threading.Tasks.ValueTask DisposeAsync() => default;
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_a_base_class_Dispose_selects_the_derived_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public abstract class Base : System.IDisposable
            {
                public void Dispose() => N.Widget.Prepare();
            }

            public sealed class WidgetTests : Base
            {
                [Xunit.Fact]
                public void First() { }
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_a_class_fixture_selects_the_class()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public sealed class Database : System.IDisposable
            {
                public Database() => N.Widget.Prepare();

                public void Dispose() => N.Widget.Spin();
            }

            public class WidgetTests : Xunit.IClassFixture<Database>
            {
                [Xunit.Fact]
                public void First() { }
            }

            public class OtherTests
            {
                [Xunit.Fact]
                public void Elsewhere() { }
            }
            """,
            changed: ["Widget.Prepare", "Widget.Spin"]);

        // The fixture is constructed by the framework and handed to nothing: no instruction
        // anywhere names its constructor.
        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_a_collection_fixture_selects_the_collection()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public sealed class Database
            {
                public Database() => N.Widget.Prepare();
            }

            [Xunit.CollectionDefinition("db")]
            public class DatabaseCollection : Xunit.ICollectionFixture<Database> { }

            [Xunit.Collection("db")]
            public class WidgetTests
            {
                [Xunit.Fact]
                public void First() { }
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_an_assembly_fixture_selects_every_test()
    {
        using var selections = Selections.Of(
            Core,
            """
            [assembly: Xunit.AssemblyFixture(typeof(N.Tests.Database))]

            namespace N.Tests;

            public sealed class Database
            {
                public Database() => N.Widget.Prepare();
            }

            public class WidgetTests
            {
                [Xunit.Fact]
                public void First() { }
            }

            public class OtherTests
            {
                [Xunit.Fact]
                public void Elsewhere() { }
            }
            """,
            changed: ["Widget.Prepare"]);

        Assert.Equal(["Elsewhere", "First"], selections.SelectedNames);
    }

    [Fact]
    public void The_lifecycle_hop_appears_in_the_path()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public class WidgetTests
            {
                public WidgetTests() => N.Widget.Prepare();

                [Xunit.Fact]
                public void First() { }
            }
            """,
            changed: ["Widget.Prepare"],
            includePaths: true);

        var paths = Assert.Single(selections.TestProject.Selected).Paths;

        Assert.NotNull(paths);

        // First -> .ctor -> Prepare.
        Assert.Equal(3, Assert.Single(paths).Count);
    }

    [Fact]
    public void Lifecycle_edges_carry_synthesised_provenance()
    {
        Assert.True(EdgeProvenance.TestLifecycle.IsSynthesised());
        Assert.False(EdgeProvenance.TestLifecycle.IsWidened());
        Assert.False(EdgeProvenance.TestLifecycle.IsCompiled());
    }

    // ---- NUnit -----------------------------------------------------------------------------------

    [Theory]
    [InlineData("SetUp")]
    [InlineData("TearDown")]
    [InlineData("OneTimeSetUp")]
    [InlineData("OneTimeTearDown")]
    public void A_change_reached_only_from_an_NUnit_hook_selects_the_fixture(string hook)
    {
        using var selections = Selections.Of(
            Core,
            $$"""
            namespace N.Tests;

            public class WidgetTests
            {
                [NUnit.Framework.{{hook}}]
                public void Hook() => N.Widget.Prepare();

                [NUnit.Framework.Test]
                public void First() { }
            }

            public class OtherTests
            {
                [NUnit.Framework.Test]
                public void Elsewhere() { }
            }
            """,
            changed: ["Widget.Prepare"],
            testFramework: NUnitFramework.Value);

        Assert.Equal("nunit", selections.TestProject.Dialect);
        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_an_inherited_NUnit_SetUp_selects_the_derived_fixture()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests;

            public abstract class Base
            {
                [NUnit.Framework.SetUp]
                public void Hook() => N.Widget.Prepare();
            }

            public class WidgetTests : Base
            {
                [NUnit.Framework.Test]
                public void First() { }
            }
            """,
            changed: ["Widget.Prepare"],
            testFramework: NUnitFramework.Value);

        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Fact]
    public void A_change_reached_only_from_an_NUnit_SetUpFixture_selects_every_test()
    {
        using var selections = Selections.Of(
            Core,
            """
            namespace N.Tests
            {
                [NUnit.Framework.SetUpFixture]
                public class Global
                {
                    [NUnit.Framework.OneTimeSetUp]
                    public void Before() => N.Widget.Prepare();
                }

                public class WidgetTests
                {
                    [NUnit.Framework.Test]
                    public void First() { }
                }
            }

            namespace N.Tests.Deeper
            {
                public class OtherTests
                {
                    [NUnit.Framework.Test]
                    public void Elsewhere() { }
                }
            }
            """,
            changed: ["Widget.Prepare"],
            testFramework: NUnitFramework.Value);

        Assert.Equal(["Elsewhere", "First"], selections.SelectedNames);
    }

    // ---- MSTest ----------------------------------------------------------------------------------

    [Theory]
    [InlineData("TestInitialize", "public void")]
    [InlineData("TestCleanup", "public void")]
    [InlineData("ClassInitialize", "public static void")]
    [InlineData("ClassCleanup", "public static void")]
    public void A_change_reached_only_from_an_MSTest_class_hook_selects_the_class(string hook, string modifiers)
    {
        using var selections = Selections.Of(
            Core,
            $$"""
            namespace N.Tests;

            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class WidgetTests
            {
                [{{hook}}]
                {{modifiers}} Hook() => N.Widget.Prepare();

                [TestMethod]
                public void First() { }
            }

            [TestClass]
            public class OtherTests
            {
                [TestMethod]
                public void Elsewhere() { }
            }
            """,
            changed: ["Widget.Prepare"],
            testFramework: MsTestFramework.Value);

        Assert.Equal("mstest", selections.TestProject.Dialect);
        Assert.Equal(["First"], selections.SelectedNames);
    }

    [Theory]
    [InlineData("AssemblyInitialize")]
    [InlineData("AssemblyCleanup")]
    public void A_change_reached_only_from_an_MSTest_assembly_hook_selects_every_test(string hook)
    {
        using var selections = Selections.Of(
            Core,
            $$"""
            namespace N.Tests;

            using Microsoft.VisualStudio.TestTools.UnitTesting;

            [TestClass]
            public class Global
            {
                [{{hook}}]
                public static void Hook() => N.Widget.Prepare();
            }

            [TestClass]
            public class WidgetTests
            {
                [TestMethod]
                public void First() { }
            }

            [TestClass]
            public class OtherTests
            {
                [TestMethod]
                public void Elsewhere() { }
            }
            """,
            changed: ["Widget.Prepare"],
            testFramework: MsTestFramework.Value);

        Assert.Equal(["Elsewhere", "First"], selections.SelectedNames);
    }

    // ---- Stand-in frameworks ---------------------------------------------------------------------

    /// <summary>
    /// Recognition reads the referenced identity, so a stand-in with the real name and version
    /// is recognised exactly as the real package would be.
    /// </summary>
    private static readonly Lazy<CompiledAssembly> NUnitFramework = new(() => Compiled.Assembly(
        "nunit.framework",
        """
        [assembly: System.Reflection.AssemblyVersion("4.2.2.0")]

        namespace NUnit.Framework;

        public sealed class TestAttribute : System.Attribute { }
        public sealed class SetUpAttribute : System.Attribute { }
        public sealed class TearDownAttribute : System.Attribute { }
        public sealed class OneTimeSetUpAttribute : System.Attribute { }
        public sealed class OneTimeTearDownAttribute : System.Attribute { }
        public sealed class SetUpFixtureAttribute : System.Attribute { }
        """,
        "/stand-in/NUnit.cs"));

    private static readonly Lazy<CompiledAssembly> MsTestFramework = new(() => Compiled.Assembly(
        "Microsoft.VisualStudio.TestPlatform.TestFramework",
        """
        [assembly: System.Reflection.AssemblyVersion("3.6.0.0")]

        namespace Microsoft.VisualStudio.TestTools.UnitTesting;

        public sealed class TestClassAttribute : System.Attribute { }
        public sealed class TestMethodAttribute : System.Attribute { }
        public sealed class TestInitializeAttribute : System.Attribute { }
        public sealed class TestCleanupAttribute : System.Attribute { }
        public sealed class ClassInitializeAttribute : System.Attribute { }
        public sealed class ClassCleanupAttribute : System.Attribute { }
        public sealed class AssemblyInitializeAttribute : System.Attribute { }
        public sealed class AssemblyCleanupAttribute : System.Attribute { }
        """,
        "/stand-in/MSTest.cs"));
}
