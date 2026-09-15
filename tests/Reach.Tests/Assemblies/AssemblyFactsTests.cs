using Reach.Assemblies;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Assemblies;

/// <summary>
/// What an assembly says about itself, read from real IL compiled in memory.
/// </summary>
public class AssemblyFactsTests
{
    private const string Source = "namespace N; public class Widget { public int Spin() => 1; }";

    [Fact]
    public void The_simple_name_comes_from_metadata_not_from_the_file()
    {
        var compiled = Compiled.Assembly("Contoso.Core", Source, "/repo/src/Widget.cs");

        Assert.Equal("Contoso.Core", compiled.Read(AssemblyFacts.SimpleName));
    }

    [Fact]
    public void The_target_framework_comes_from_the_assemblys_own_attribute()
    {
        var compiled = Compiled.Assembly(
            "Core", Source, "/repo/src/Widget.cs", ".NETCoreApp,Version=v8.0");

        // The payoff for scanning rather than predicting: the target framework is exactly what
        // every ambiguous layout destroys on disk.
        Assert.Equal(".NETCoreApp,Version=v8.0", compiled.Read(AssemblyFacts.TargetFramework));
    }

    [Fact]
    public void The_target_platform_is_read_too()
    {
        var compiled = Compiled.Assembly(
            "Core", Source, "/repo/src/Widget.cs", ".NETCoreApp,Version=v10.0", "Windows7.0");

        Assert.Equal("Windows7.0", compiled.Read(AssemblyFacts.TargetPlatform));
    }

    [Fact]
    public void An_assembly_with_no_platform_attribute_reports_none()
    {
        var compiled = Compiled.Assembly("Core", Source, "/repo/src/Widget.cs");

        Assert.Null(compiled.Read(AssemblyFacts.TargetPlatform));
    }

    [Fact]
    public void Two_compilations_of_the_same_source_have_different_module_version_ids()
    {
        var first = Compiled.Assembly("Core", Source, "/repo/src/Widget.cs");
        var second = Compiled.Assembly("Core", Source, "/repo/src/Widget.cs");

        // Which is what makes the module version id a usable answer to "is this the same build
        // copied twice, or two builds?".
        Assert.NotEqual(first.Read(AssemblyFacts.ModuleVersionId), second.Read(AssemblyFacts.ModuleVersionId));
    }

    [Fact]
    public void The_symbols_record_the_document_the_source_came_from()
    {
        using var directory = TempDirectory.Create("reach-pdb");

        var path = Compiled
            .Assembly("Core", Source, "/repo/src/Widget.cs")
            .WriteTo(directory.Path);

        var scanned = AssemblyScanner.Read(path, "/repo");

        Assert.NotNull(scanned);
        Assert.Equal(["/repo/src/Widget.cs"], scanned.Documents.Select(document => document.Path));
    }

    [Fact]
    public void Every_document_carries_the_checksum_the_compiler_recorded()
    {
        using var directory = TempDirectory.Create("reach-pdb");

        var path = Compiled
            .Assembly("Core", Source, "/repo/src/Widget.cs")
            .WriteTo(directory.Path);

        var document = Assert.Single(AssemblyScanner.Read(path, "/repo")!.Documents);

        Assert.True(document.HasChecksum);
        Assert.NotEqual(Guid.Empty, document.HashAlgorithm);
    }
}

/// <summary>
/// Turning a declared moniker and a stamped attribute into the same shape. Neither spelling
/// can be derived from the other, so the comparison happens in a third form.
/// </summary>
public class TargetFrameworkMonikerTests
{
    [Theory]
    [InlineData("net10.0", ".NETCoreApp,Version=v10.0", null)]
    [InlineData("net8.0", ".NETCoreApp,Version=v8.0", null)]
    [InlineData("netstandard2.0", ".NETStandard,Version=v2.0", null)]
    [InlineData("netcoreapp3.1", ".NETCoreApp,Version=v3.1", null)]
    [InlineData("net472", ".NETFramework,Version=v4.7.2", null)]
    [InlineData("net48", ".NETFramework,Version=v4.8", null)]
    [InlineData("net10.0-windows", ".NETCoreApp,Version=v10.0", "Windows7.0")]
    [InlineData("net10.0-android", ".NETCoreApp,Version=v10.0", "Android21.0")]
    public void A_declared_moniker_matches_the_attributes_the_SDK_stamps(
        string declared,
        string frameworkName,
        string? platform)
    {
        var fromProject = TargetFrameworkMoniker.Parse(declared);
        var fromAssembly = TargetFrameworkMoniker.FromAttributes(frameworkName, platform);

        Assert.NotNull(fromProject);
        Assert.NotNull(fromAssembly);
        Assert.True(fromProject.Matches(fromAssembly), $"{fromProject} did not match {fromAssembly}");
    }

    [Fact]
    public void A_platform_suffixed_moniker_does_not_match_a_plain_one()
    {
        var plain = TargetFrameworkMoniker.Parse("net10.0")!;
        var windows = TargetFrameworkMoniker.FromAttributes(".NETCoreApp,Version=v10.0", "Windows7.0")!;

        // Which is what keeps net10.0 and net10.0-windows two instances rather than one
        // ambiguity error.
        Assert.False(plain.Matches(windows));
        Assert.False(windows.Matches(plain));
    }

    [Fact]
    public void The_declared_platform_version_is_ignored_because_the_attribute_always_carries_one()
    {
        // net10.0-windows and net10.0-windows7.0 stamp byte-identical attributes, so the
        // declared version cannot be recovered and must not be compared.
        Assert.True(
            TargetFrameworkMoniker.Parse("net10.0-windows7.0")!
                .Matches(TargetFrameworkMoniker.Parse("net10.0-windows")!));
    }

    [Fact]
    public void Different_versions_do_not_match() =>
        Assert.False(
            TargetFrameworkMoniker.Parse("net10.0")!
                .Matches(TargetFrameworkMoniker.FromAttributes(".NETCoreApp,Version=v8.0", null)!));

    [Fact]
    public void Different_identifiers_do_not_match() =>
        Assert.False(
            TargetFrameworkMoniker.Parse("netstandard2.0")!
                .Matches(TargetFrameworkMoniker.FromAttributes(".NETCoreApp,Version=v2.0", null)!));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    public void An_unreadable_moniker_is_null(string declared) =>
        Assert.Null(TargetFrameworkMoniker.Parse(declared));
}
