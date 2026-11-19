using System.Collections.Immutable;
using System.Reflection.Metadata;
using Reach.Assemblies;
using Reach.Git;
using Reach.Processes;
using Reach.Projects;
using Reach.Reporting;
using Reach.Tests.Fixtures;

namespace Reach.Tests.Assemblies;

/// <summary>
/// The correspondence check itself, over assemblies compiled in memory and written where the
/// test wants them.
/// </summary>
public class CorrespondenceTests
{
    private const string Source = "namespace N; public class Widget { public int Spin() => 1; }";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Writes <paramref name="source"/> to disk, compiles it from exactly those bytes, and
    /// returns the assembly instance discovery would have produced.
    /// </summary>
    private static AssemblyInstance Instance(
        TempDirectory directory,
        string source = Source,
        string fileName = "Widget.cs",
        bool embeddedSymbols = false)
    {
        var documentPath = Path.Combine(directory.Path, "src", fileName);

        Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
        File.WriteAllText(documentPath, source);

        var compiled = Compiled.Assembly("Core", source, documentPath, embedSymbols: embeddedSymbols);
        var assemblyPath = compiled.WriteTo(Path.Combine(directory.Path, "bin"));

        var scanned = AssemblyScanner.Read(assemblyPath, directory.Path);
        Assert.NotNull(scanned);

        var project = ProjectFileReader.Read(WriteProject(directory));

        return new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned);
    }

    private static string WriteProject(TempDirectory directory)
    {
        var path = Path.Combine(directory.Path, "src", "Core.csproj");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);

        return path;
    }

    // ---- The three verdicts -------------------------------------------------------------

    [Fact]
    public void Source_that_matches_the_recorded_checksums_verifies()
    {
        using var directory = TempDirectory.Create("reach-correspondence");

        var result = Correspondence.Verify([Instance(directory)], SourceVisibility.Unknown);

        Assert.Equal(CorrespondenceVerdict.Verified, result.Verdict);
        Assert.Equal(ExitCode.Success, result.ExitCode);
        Assert.Contains(result.Documents, verdict => verdict.Class == DocumentClass.Verified);
    }

    [Fact]
    public void Source_edited_after_the_build_is_exit_5_naming_the_document()
    {
        using var directory = TempDirectory.Create("reach-correspondence");

        var instance = Instance(directory);

        // Exactly the --no-build hazard: the binaries belong to a different revision of this
        // file, and no timestamp would say so — git does not preserve them.
        var document = instance.Assembly.Documents.First(d => File.Exists(d.Path));
        File.AppendAllText(document.Path, "\n// edited after the build\n");

        var result = Correspondence.Verify([instance], SourceVisibility.Unknown);

        Assert.Equal(CorrespondenceVerdict.Failed, result.Verdict);
        Assert.Equal(ExitCode.CorrespondenceFailed, result.ExitCode);
        Assert.Contains(Path.GetFileName(document.Path), result.Message);
        Assert.Contains("--no-build", result.Message);
    }

    [Fact]
    public void Nothing_to_check_is_skipped_rather_than_verified()
    {
        using var directory = TempDirectory.Create("reach-correspondence");

        var instance = Instance(directory);

        // No symbols at all, so no documents: the honest answer is that nothing was proved.
        var result = Correspondence.Verify(
            [instance with { Assembly = instance.Assembly with { Documents = [] } }],
            SourceVisibility.Unknown);

        Assert.Equal(CorrespondenceVerdict.Skipped, result.Verdict);
        Assert.Equal(ExitCode.Success, result.ExitCode);
    }

    // ---- Symbol shapes ---------------------------------------------------------------------

    [Fact]
    public void An_assembly_with_embedded_symbols_verifies_identically()
    {
        using var directory = TempDirectory.Create("reach-embedded");

        var instance = Instance(directory, embeddedSymbols: true);

        // Nothing beside the assembly to read: the documents come out of the image itself.
        Assert.False(File.Exists(Path.ChangeExtension(instance.Assembly.Path, ".pdb")));
        Assert.NotEmpty(instance.Assembly.Documents);

        Assert.Equal(
            CorrespondenceVerdict.Verified,
            Correspondence.Verify([instance], SourceVisibility.Unknown).Verdict);
    }

    [Fact]
    public void An_edited_file_fails_the_check_with_embedded_symbols_too()
    {
        using var directory = TempDirectory.Create("reach-embedded");

        var instance = Instance(directory, embeddedSymbols: true);
        File.AppendAllText(
            instance.Assembly.Documents.First(d => File.Exists(d.Path)).Path,
            "\n// edited\n");

        Assert.Equal(
            CorrespondenceVerdict.Failed,
            Correspondence.Verify([instance], SourceVisibility.Unknown).Verdict);
    }

    [Fact]
    public void A_document_with_no_file_on_disk_is_generated_not_a_failure()
    {
        using var directory = TempDirectory.Create("reach-correspondence");

        var instance = Instance(directory);
        var document = instance.Assembly.Documents.First(d => File.Exists(d.Path));

        File.Delete(document.Path);

        var result = Correspondence.Verify([instance], SourceVisibility.Unknown);

        // A source generator's output has no file to hash, and needs an explicit skip rather
        // than reading as a missing file.
        Assert.NotEqual(CorrespondenceVerdict.Failed, result.Verdict);
        Assert.Contains(result.Documents, verdict => verdict.Class == DocumentClass.Generated);
    }

    [Fact]
    public void One_document_shared_by_several_instances_is_checked_once()
    {
        using var directory = TempDirectory.Create("reach-correspondence");

        var instance = Instance(directory);

        var result = Correspondence.Verify([instance, instance], SourceVisibility.Unknown);

        // Which any multi-targeted project produces, and which is also what keeps the report
        // from listing the same file twice.
        Assert.Equal(
            instance.Assembly.Documents.Select(document => document.Path).Distinct().Count(),
            result.Documents.Count);
    }

    // ---- Visibility -------------------------------------------------------------------------

    [Fact]
    public async Task A_first_party_document_that_is_untracked_and_ignored_is_disclosed()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile(".gitignore", "src/Generated.cs\n");
        repository.WriteFile("src/Tracked.cs", "namespace N; public class Tracked { }");
        repository.Commit("baseline");

        var generated = repository.WriteFile(
            "src/Generated.cs",
            "namespace N; public class Generated { public int M() => 1; }");

        var compiled = Compiled.Assembly(
            "Core",
            [
                ("namespace N; public class Tracked { }", repository.Combine("src", "Tracked.cs")),
                ("namespace N; public class Generated { public int M() => 1; }", generated),
            ]);

        var scanned = AssemblyScanner.Read(
            compiled.WriteTo(repository.Combine("bin")),
            repository.Path);

        var visibility = await SourceVisibility.ReadAsync(
            new GitAdapter(new ProcessRunner(), repository.Path),
            repository.Path,
            Token);

        var project = ProjectFileReader.Read(repository.WriteFile(
            "src/Core.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"));

        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned!)],
            visibility);

        // .gitignore governs only untracked files, so the blind spot is precisely
        // untracked-AND-ignored. Reported, never selected on: selecting would widen every
        // project's assembly on every run.
        var notice = Assert.Single(result.Notices);
        Assert.Equal(NoticeCodes.IgnoredUntrackedAssembly, notice.Code);
        Assert.Equal(NoticeKind.BlindSpot, notice.Kind);
        Assert.Contains("Generated.cs", notice.Message);
        Assert.DoesNotContain("Tracked.cs", notice.Message);

        // And it is still a correspondence success: the file matched its checksum.
        Assert.Equal(CorrespondenceVerdict.Verified, result.Verdict);
    }

    [Fact]
    public async Task Documents_under_obj_and_bin_emit_no_notice()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile(".gitignore", "obj/\nbin/\n");
        repository.Commit("baseline");

        var generated = repository.WriteFile(
            "src/Core/obj/Debug/net10.0/Generated.cs",
            "namespace N; public class Generated { public int M() => 1; }");

        var compiled = Compiled.Assembly(
            "Core",
            "namespace N; public class Generated { public int M() => 1; }",
            generated);

        var scanned = AssemblyScanner.Read(
            compiled.WriteTo(repository.Combine("output")),
            repository.Path);

        var visibility = await SourceVisibility.ReadAsync(
            new GitAdapter(new ProcessRunner(), repository.Path),
            repository.Path,
            Token);

        var project = ProjectFileReader.Read(repository.WriteFile(
            "src/Core/Core.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>"));

        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned!)],
            visibility);

        // obj/ lands in the untracked-and-ignored category for every project, so without the
        // suppression the notice would fire on every run. An approximation Reach is stuck
        // with, and one that can only ever cost a warning.
        Assert.Empty(result.Notices);
    }

    [Fact]
    public async Task Tracked_files_stay_visible_when_git_runs_from_the_target_project_directory()
    {
        using var repository = TempRepository.Create();

        repository.WriteFile("src/Core/Widget.cs", "namespace N; public class Widget { }");
        repository.WriteFile("tests/Tests/WidgetTests.cs", "namespace N; public class WidgetTests { }");
        repository.Commit("baseline");
        repository.WriteFile("src/Core/Untracked.cs", "namespace N; public class Untracked { }");

        // The pipeline runs git in the target's directory, which for a test project is a
        // subdirectory of the repository. git ls-files lists only that subtree, spelled
        // relative to it, unless asked for the whole repository by its full names.
        var visibility = await SourceVisibility.ReadAsync(
            new GitAdapter(new ProcessRunner(), repository.Combine("tests", "Tests")),
            repository.Path,
            Token);

        Assert.False(visibility.IsInvisible(repository.Combine("src", "Core", "Widget.cs")));
        Assert.False(visibility.IsInvisible(repository.Combine("tests", "Tests", "WidgetTests.cs")));
        Assert.False(visibility.IsInvisible(repository.Combine("src", "Core", "Untracked.cs")));
    }

    [Fact]
    public void An_unknown_visibility_reports_nothing_as_invisible()
    {
        Assert.False(SourceVisibility.Unknown.IsInvisible("/anywhere/at/all.cs"));
    }

    // ---- Paths -------------------------------------------------------------------------------

    [Fact]
    public void A_document_recorded_with_mixed_separators_still_finds_its_file()
    {
        using var directory = TempDirectory.Create("reach-separators");

        var source = Source;
        var nested = Path.Combine(directory.Path, "src", "deep");

        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "Widget.cs"), source);

        // As a solution written on Windows and read on Linux would carry it.
        var mixed = directory.Path + "/src\\deep/Widget.cs";

        var compiled = Compiled.Assembly("Core", source, mixed);
        var scanned = AssemblyScanner.Read(compiled.WriteTo(directory.Combine("bin")), directory.Path);

        Assert.NotNull(scanned);

        var project = ProjectFileReader.Read(WriteProject(directory));
        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned)],
            SourceVisibility.Unknown);

        // A path-comparison bug here is silent under-selection rather than a crash, which is
        // why CI runs both operating systems.
        Assert.Equal(CorrespondenceVerdict.Verified, result.Verdict);
    }

    // ---- Deterministic source paths ------------------------------------------------------------

    [Fact]
    public void A_ci_build_that_records_the_repository_root_as_a_deterministic_prefix_verifies()
    {
        using var directory = TempDirectory.Create("reach-deterministic");

        var documentPath = directory.Combine("src", "Widget.cs");

        Directory.CreateDirectory(Path.GetDirectoryName(documentPath)!);
        File.WriteAllText(documentPath, Source);

        // What ContinuousIntegrationBuild does: the SDK maps the git top-level to /_/.
        var compiled = Compiled.Assembly(
            "Core",
            [(Source, documentPath)],
            pathMap: [(directory.Path + Path.DirectorySeparatorChar, "/_/")]);

        Assert.Contains("/_/src/Widget.cs", RecordedDocuments(compiled));

        var scanned = AssemblyScanner.Read(compiled.WriteTo(directory.Combine("bin")), directory.Path);

        Assert.NotNull(scanned);
        Assert.True(scanned.IsFirstParty);
        Assert.Contains(scanned.Documents, document => Paths.Same(document.Path, documentPath));

        var project = ProjectFileReader.Read(WriteProject(directory));
        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned)],
            SourceVisibility.Unknown);

        Assert.Equal(CorrespondenceVerdict.Verified, result.Verdict);
        Assert.Empty(result.Notices);
    }

    [Fact]
    public void A_further_deterministic_root_is_not_guessed_at_and_says_so()
    {
        using var directory = TempDirectory.Create("reach-deterministic");

        var widget = directory.Combine("src", "Widget.cs");
        var helper = directory.Combine("lib", "Helper.cs");
        const string helperSource = "namespace N; public static class Helper { public static int One() => 1; }";

        Directory.CreateDirectory(Path.GetDirectoryName(widget)!);
        Directory.CreateDirectory(Path.GetDirectoryName(helper)!);
        File.WriteAllText(widget, Source);
        File.WriteAllText(helper, helperSource);

        // A second top-level SourceRoot the build added itself. Nothing in the symbols says
        // which directory /_1/ was, and it is not a submodule: those nest under /_/.
        var compiled = Compiled.Assembly(
            "Core",
            [(Source, widget), (helperSource, helper)],
            pathMap:
            [
                (directory.Combine("lib") + Path.DirectorySeparatorChar, "/_1/"),
                (directory.Path + Path.DirectorySeparatorChar, "/_/"),
            ]);

        Assert.Contains("/_1/Helper.cs", RecordedDocuments(compiled));

        var scanned = AssemblyScanner.Read(
            compiled.WriteTo(directory.Combine("bin")),
            new SourceRoots(directory.Path, directory.Combine("packages")));

        Assert.NotNull(scanned);
        Assert.True(scanned.IsFirstParty);
        Assert.DoesNotContain(scanned.Documents, document => Paths.Same(document.Path, helper));

        var project = ProjectFileReader.Read(WriteProject(directory));
        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned)],
            SourceVisibility.Unknown);

        // A change to Helper.cs could not be traced, so the run says so rather than guessing
        // a directory and hoping the checksum agrees.
        var notice = Assert.Single(result.Notices);
        Assert.Equal(NoticeCodes.UnmappedDeterministicSourceRoot, notice.Code);
        Assert.Equal(NoticeKind.BlindSpot, notice.Kind);
        Assert.Contains("/_1/", notice.Message);
        Assert.Contains("DeterministicSourcePaths", notice.Message);
        Assert.Equal(CorrespondenceVerdict.Verified, result.Verdict);
    }

    [Fact]
    public void A_deterministic_root_that_was_not_the_repository_fails_naming_the_cause()
    {
        using var directory = TempDirectory.Create("reach-deterministic");

        var widget = directory.Combine("src", "Core", "Widget.cs");

        Directory.CreateDirectory(Path.GetDirectoryName(widget)!);
        File.WriteAllText(widget, Source);

        // A hand-written SourceRoot of src/ with source-control queries off: the build gave /_/
        // to src/, so /_/Core/Widget.cs read as the repository root names no file.
        var compiled = Compiled.Assembly(
            "Core",
            [(Source, widget)],
            pathMap: [(directory.Combine("src") + Path.DirectorySeparatorChar, "/_/")]);

        Assert.Contains("/_/Core/Widget.cs", RecordedDocuments(compiled));

        var scanned = AssemblyScanner.Read(
            compiled.WriteTo(directory.Combine("bin")),
            new SourceRoots(directory.Path, Packages: null));

        Assert.NotNull(scanned);

        var project = ProjectFileReader.Read(WriteProject(directory));
        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned)],
            SourceVisibility.Unknown);

        // Not "nothing to check": every source file the assembly names is missing, which a
        // correct mapping cannot produce, so the run stops and says why.
        Assert.Equal(CorrespondenceVerdict.Failed, result.Verdict);
        Assert.Equal(ExitCode.CorrespondenceFailed, result.ExitCode);
        Assert.Contains("Core", result.Message);
        Assert.Contains("/_/", result.Message);
        Assert.Contains("SourceRoot", result.Message);
        Assert.Contains("-p:DeterministicSourcePaths=false", result.Message);
    }

    [Fact]
    public void A_deterministic_build_with_generated_documents_missing_still_verifies()
    {
        using var directory = TempDirectory.Create("reach-deterministic");

        var widget = directory.Combine("src", "Widget.cs");
        const string generatedSource = "namespace N; internal static class Generated { public static int One() => 1; }";

        Directory.CreateDirectory(Path.GetDirectoryName(widget)!);
        File.WriteAllText(widget, Source);

        // A source generator's output: recorded under /_/ like everything else, never on disk.
        var compiled = Compiled.Assembly(
            "Core",
            [(Source, widget), (generatedSource, directory.Combine("obj", "Gen", "Generated.g.cs"))],
            pathMap: [(directory.Path + Path.DirectorySeparatorChar, "/_/")]);

        var scanned = AssemblyScanner.Read(
            compiled.WriteTo(directory.Combine("bin")),
            new SourceRoots(directory.Path, Packages: null));

        Assert.NotNull(scanned);

        var project = ProjectFileReader.Read(WriteProject(directory));
        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned)],
            SourceVisibility.Unknown);

        Assert.Equal(CorrespondenceVerdict.Verified, result.Verdict);
        Assert.Contains(result.Documents, verdict => verdict.Class == DocumentClass.Generated);
    }

    [Fact]
    public void Package_content_under_a_further_root_is_found_in_the_packages_folder()
    {
        using var directory = TempDirectory.Create("reach-deterministic");

        var widget = directory.Combine("src", "Widget.cs");
        var packages = directory.Combine("packages");
        var content = Path.Combine(packages, "some.package", "1.0.0", "_content", "Reporter.cs");
        const string contentSource = "namespace N; internal static class Reporter { public static int One() => 1; }";

        Directory.CreateDirectory(Path.GetDirectoryName(widget)!);
        Directory.CreateDirectory(Path.GetDirectoryName(content)!);
        File.WriteAllText(widget, Source);
        File.WriteAllText(content, contentSource);

        // What restore does: every package folder is a SourceRoot, so a content file a package
        // compiles into the project is recorded under /_1/ — xunit.v3's runner does this.
        var compiled = Compiled.Assembly(
            "Core",
            [(Source, widget), (contentSource, content)],
            pathMap:
            [
                (packages + Path.DirectorySeparatorChar, "/_1/"),
                (directory.Path + Path.DirectorySeparatorChar, "/_/"),
            ]);

        Assert.Contains("/_1/some.package/1.0.0/_content/Reporter.cs", RecordedDocuments(compiled));

        var scanned = AssemblyScanner.Read(
            compiled.WriteTo(directory.Combine("bin")),
            new SourceRoots(directory.Path, packages));

        Assert.NotNull(scanned);
        Assert.Contains(scanned.Documents, document => Paths.Same(document.Path, content));

        var project = ProjectFileReader.Read(WriteProject(directory));
        var result = Correspondence.Verify(
            [new AssemblyInstance(new ExpectedAssemblyInstance(project, "net10.0"), scanned)],
            SourceVisibility.Unknown);

        // Found by looking, then checked like any other file: no gap to disclose.
        Assert.Empty(result.Notices);
        Assert.Equal(CorrespondenceVerdict.Verified, result.Verdict);
        Assert.Equal(2, result.Documents.Count(verdict => verdict.Class == DocumentClass.Verified));
    }

    /// <summary>
    /// The document names exactly as the symbols record them, read independently of Reach.
    /// </summary>
    private static string[] RecordedDocuments(CompiledAssembly compiled)
    {
        using var provider = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(compiled.Symbols));
        var reader = provider.GetMetadataReader();

        return [.. reader.Documents.Select(handle => reader.GetString(reader.GetDocument(handle).Name))];
    }
}
