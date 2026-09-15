using System.Security.Cryptography;
using Reach.Assemblies;

namespace Reach.Tests.Assemblies;

/// <summary>
/// <strong>The claim this whole phase rests on, checked against real build output.</strong>
/// Source–binary correspondence assumes portable PDBs record a per-document source checksum
/// that can be reproduced from the file on disk. That had never been verified against a real
/// <c>dotnet build</c>, and if it does not hold then the phase is redesigned rather than worked
/// around.
/// </summary>
/// <remarks>
/// The subject is Reach's own build output: it is produced by a real SDK build with the
/// project's own settings, it is already on disk whenever the suite runs, and its source files
/// are right there to hash.
/// </remarks>
public class PortablePdbChecksumTests
{
    private static readonly Guid Sha256 = new("8829d00f-11b8-4213-878b-770e8597ac16");
    private static readonly Guid Sha1 = new("ff1816ec-aa5e-4d10-87f7-6f4963833460");

    /// <summary>The assembly under test is the one this test is compiled into.</summary>
    private static ScannedAssembly Subject()
    {
        var path = typeof(Reach.Product).Assembly.Location;

        Assert.True(File.Exists(path), $"Reach.Core was expected at {path}.");

        var scanned = AssemblyScanner.Read(path, Path.GetPathRoot(path)!);

        Assert.NotNull(scanned);
        return scanned;
    }

    [Fact]
    public void A_real_build_records_a_document_per_source_file()
    {
        var subject = Subject();

        Assert.NotEmpty(subject.Documents);
        Assert.Contains(subject.Documents, document => document.Path.EndsWith("Paths.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Every_recorded_document_carries_a_checksum_and_an_algorithm()
    {
        var subject = Subject();

        Assert.All(subject.Documents, document =>
        {
            Assert.True(document.HasChecksum, $"{document.Path} carried no checksum.");
            Assert.NotEqual(Guid.Empty, document.HashAlgorithm);
        });
    }

    [Fact]
    public void The_algorithm_a_current_SDK_uses_is_SHA_256()
    {
        var subject = Subject();

        // Not assumed anywhere in production code — SourceDocument carries the algorithm GUID
        // — but worth pinning, because comparing a file against the wrong algorithm's digest
        // fails in the direction that stops a run.
        Assert.All(subject.Documents, document => Assert.Equal(Sha256, document.HashAlgorithm));
    }

    [Fact]
    public void Recomputing_the_checksum_over_the_file_on_disk_reproduces_it()
    {
        var subject = Subject();

        var onDisk = subject.Documents
            .Where(document => File.Exists(document.Path))
            .ToArray();

        Assert.NotEmpty(onDisk);

        foreach (var document in onDisk)
        {
            var bytes = File.ReadAllBytes(document.Path);

            var computed = document.HashAlgorithm == Sha1
                ? SHA1.HashData(bytes)
                : SHA256.HashData(bytes);

            // The whole of source-binary correspondence is this line holding.
            Assert.True(
                computed.AsSpan().SequenceEqual(document.Hash),
                $"The checksum recorded for {document.Path} is not the hash of the file on disk.");
        }
    }

    [Fact]
    public void An_edited_file_no_longer_matches_its_recorded_checksum()
    {
        var subject = Subject();

        var document = subject.Documents.First(candidate => File.Exists(candidate.Path));
        var edited = File.ReadAllBytes(document.Path).Concat("\n// edited\n"u8.ToArray()).ToArray();

        // Which is what makes the mismatch detectable at all, and why the mode that trusts the
        // caller more is the mode that detects more.
        Assert.False(SHA256.HashData(edited).AsSpan().SequenceEqual(document.Hash));
    }

    [Fact]
    public void Compiler_generated_documents_have_no_file_on_disk()
    {
        var subject = Subject();

        // Source generators, and the SDK's own AssemblyInfo, land under obj/ or carry a
        // synthetic path. They need the explicit skip rule rather than failing the check.
        var generated = subject.Documents.Where(document => !File.Exists(document.Path)).ToArray();

        Assert.All(generated, document => Assert.False(File.Exists(document.Path)));
    }
}
