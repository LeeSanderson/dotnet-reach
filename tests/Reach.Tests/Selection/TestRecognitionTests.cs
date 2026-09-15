using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Reach.Assemblies;
using Reach.Graph;
using Reach.Selection;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Selection;

/// <summary>
/// Recognition is data, and the data is asserted against real referenced identities rather
/// than against the table restating itself.
/// </summary>
public class TestRecognitionTests
{
    [Fact]
    public void The_suites_own_assembly_is_recognised_as_xUnit_v3()
    {
        var path = typeof(TestRecognitionTests).Assembly.Location;

        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream, PEStreamOptions.PrefetchEntireImage);

        var framework = TestFrameworks.DetectedIn(reader.GetMetadataReader());

        // The one assertion in this file against a real build rather than a synthesised
        // reference: it is what keeps the table honest about what a current SDK produces.
        Assert.NotNull(framework);
        Assert.Equal("xUnit v3", framework.Name);
    }

    [Fact]
    public void Its_tests_are_enumerated_from_their_attributes()
    {
        var path = typeof(TestRecognitionTests).Assembly.Location;

        using var stream = File.OpenRead(path);
        using var reader = new PEReader(stream, PEStreamOptions.PrefetchEntireImage);

        var metadata = reader.GetMetadataReader();
        var framework = TestFrameworks.DetectedIn(metadata)!;

        var assembly = new GraphAssembly(
            0,
            "Reach.Tests",
            TargetFrameworkMoniker.Parse("net10.0"),
            metadata,
            reader);

        var tests = TestMethods.In(assembly, framework);

        Assert.Contains(
            tests,
            test => test.FullyQualifiedName
                == "Reach.Tests.Selection.TestRecognitionTests.Its_tests_are_enumerated_from_their_attributes");

        // A theory is one test method, never one per case. Selection granularity is the method.
        Assert.Single(
            tests,
            test => test.Name == nameof(Its_tests_are_enumerated_from_their_attributes));
    }

    [Theory]
    [InlineData("xunit.core", "2.9.3", "xUnit v2", "xunit-v2")]
    [InlineData("xunit.v3.core", "3.2.0", "xUnit v3", "xunit-v3")]
    // There is no xUnit v4: the package xunit.v3 is at *version* 4.0.0, and that release
    // changed the filter surface — so the dialect is gated on the version too.
    [InlineData("xunit.v3.core", "4.0.0", "xUnit v3", "xunit-v3-4")]
    [InlineData("nunit.framework", "4.2.2", "NUnit", "nunit")]
    [InlineData("Microsoft.VisualStudio.TestPlatform.TestFramework", "3.6.0", "MSTest", "mstest")]
    public void The_table_gates_on_framework_and_version(
        string assembly,
        string version,
        string expectedName,
        string expectedDialect)
    {
        var framework = TestFrameworks.Table
            .LastOrDefault(row => row.Covers(assembly, Version.Parse(version)));

        Assert.NotNull(framework);
        Assert.Equal(expectedName, framework.Name);
        Assert.Equal(expectedDialect, framework.Dialect);
    }

    [Fact]
    public void An_unrecognised_framework_matches_no_row() =>
        Assert.Null(
            TestFrameworks.Table.LastOrDefault(row => row.Covers("Contoso.Testing", new Version(1, 0))));

    [Fact]
    public void A_project_referencing_nothing_recognisable_is_not_a_test_project()
    {
        var compiled = Compiled.Assembly(
            "Plain",
            "namespace N; public class C { public void M() { } }",
            "/repo/C.cs");

        using var reader = new PEReader(ImmutableArray.Create(compiled.Image));

        Assert.Null(TestFrameworks.DetectedIn(reader.GetMetadataReader()));
    }

    [Fact]
    public void An_attribute_deriving_from_a_framework_attribute_still_marks_a_test()
    {
        // Ordinary in real suites, and missing it would drop every test using it, which is
        // under-selection.
        var compiled = Compiled.Assembly(
            "Suite",
            """
            namespace Xunit
            {
                public class FactAttribute : System.Attribute { }
            }

            namespace N
            {
                public sealed class IntegrationFactAttribute : Xunit.FactAttribute { }

                public class Tests
                {
                    [Xunit.Fact] public void Plain() { }
                    [N.IntegrationFact] public void Derived() { }
                    public void NotATest() { }
                }
            }
            """,
            "/repo/Tests.cs");

        using var reader = new PEReader(ImmutableArray.Create(compiled.Image));

        var assembly = new GraphAssembly(
            0,
            "Suite",
            TargetFrameworkMoniker.Parse("net10.0"),
            reader.GetMetadataReader(),
            reader);

        var tests = TestMethods.In(
            assembly,
            new TestFramework(
                "xUnit v3",
                "xunit.v3.core",
                new Version(1, 0),
                new Version(99, 0),
                ["Xunit.FactAttribute"],
                "xunit-v3-4"));

        Assert.Equal(["N.Tests.Derived", "N.Tests.Plain"], tests.Select(test => test.FullyQualifiedName));
    }
}
