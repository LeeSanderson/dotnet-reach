using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Reach.Reporting;

namespace Reach.Assemblies;

/// <summary>
/// Proves that the assemblies Reach is about to read were compiled from the source it diffed.
/// </summary>
/// <remarks>
/// <para>
/// Not a timestamp heuristic. Timestamps are untrustworthy because git does not preserve them,
/// so checking out an older commit onto a warm agent can leave source files <em>older</em> than
/// the binaries beside them, and MSBuild will correctly conclude nothing needs rebuilding while
/// the binaries belong to a different commit. A source checksum is the compiler's own record of
/// the bytes it compiled.
/// </para>
/// <para>
/// It runs in both build modes, and <strong>that inverts what people expect</strong>: the mode
/// that trusts the caller more — <c>--no-build</c> — is the mode that detects more, because
/// default mode quietly recompiles the changed file and every checksum then agrees.
/// </para>
/// </remarks>
internal static class Correspondence
{
    private static readonly Guid Sha1 = new("ff1816ec-aa5e-4d10-87f7-6f4963833460");
    private static readonly Guid Sha256 = new("8829d00f-11b8-4213-878b-770e8597ac16");
    private static readonly Guid Md5 = new("406ea660-64cf-4c82-b6f0-42d48172a799");

    /// <summary>
    /// Suppressed from the invisible-document check. <c>obj/</c> lands in that category for
    /// every project, so without this the notice would fire on every run. An approximation
    /// Reach is stuck with, because it does not run MSBuild and cannot read
    /// <c>IntermediateOutputPath</c> — and one that can only ever cost a warning, never a test.
    /// </summary>
    private static readonly string[] BuildDirectories = ["obj", "bin"];

    private static readonly char[] Separators = ['/', '\\'];

    /// <param name="instances">The assemblies whose debug-symbol documents are checked against the working tree.</param>
    /// <param name="visibility">
    /// Which paths git can see. A document that is neither tracked nor listed as an ordinary
    /// untracked file is untracked <em>and</em> ignored.
    /// </param>
    internal static CorrespondenceResult Verify(
        IReadOnlyList<AssemblyInstance> instances,
        SourceVisibility visibility)
    {
        // One document belongs to several assembly instances in any multi-targeted project.
        // Hashing it once is both faster and what keeps the report stable.
        var verdicts = instances
            .SelectMany(instance => instance.Assembly.Documents)
            .DistinctBy(document => document.Path, Paths.Comparer)
            .Select(document => new DocumentVerdict(document, Classify(document, visibility)))
            .ToList();

        var mismatched = verdicts.Where(verdict => verdict.Class == DocumentClass.Mismatched).ToArray();
        var verified = verdicts.Count(verdict => verdict.Class == DocumentClass.Verified);

        var outcome = (mismatched.Length, verified) switch
        {
            ( > 0, _) => CorrespondenceVerdict.Failed,
            (_, > 0) => CorrespondenceVerdict.Verified,
            _ => CorrespondenceVerdict.Skipped,
        };

        return new CorrespondenceResult(
            outcome,
            mismatched.Length > 0 ? Message(mismatched) : string.Empty,
            Notices(verdicts),
            verdicts);
    }

    private static DocumentClass Classify(SourceDocument document, SourceVisibility visibility)
    {
        if (!File.Exists(document.Path))
        {
            // A source generator's output has no file to hash, and needs an explicit skip
            // rather than reading as a missing file.
            return DocumentClass.Generated;
        }

        if (!document.HasChecksum || Hash(document) is not { } computed)
        {
            return DocumentClass.Unverifiable;
        }

        if (!computed.AsSpan().SequenceEqual(document.Hash))
        {
            return DocumentClass.Mismatched;
        }

        return visibility.IsInvisible(document.Path) && !IsBuildOutput(document.Path)
            ? DocumentClass.Invisible
            : DocumentClass.Verified;
    }

    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "The hash must match the algorithm the compiler recorded in the PDB; it is not used for security.")]
    [SuppressMessage("Security", "CA5351:Do Not Use Broken Cryptographic Algorithms", Justification = "The hash must match the algorithm the compiler recorded in the PDB; it is not used for security.")]
    private static byte[]? Hash(SourceDocument document)
    {
        try
        {
            var bytes = File.ReadAllBytes(document.Path);

            // Carried rather than assumed: comparing a file against the wrong algorithm's
            // digest fails in the direction that stops a run.
            return document.HashAlgorithm switch
            {
                _ when document.HashAlgorithm == Sha256 => SHA256.HashData(bytes),
                _ when document.HashAlgorithm == Sha1 => SHA1.HashData(bytes),
                _ when document.HashAlgorithm == Md5 => MD5.HashData(bytes),
                _ => null,
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool IsBuildOutput(string path) =>
        Array.Exists(
            path.Split(Separators),
            segment => BuildDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));

    private static string Message(IReadOnlyList<DocumentVerdict> mismatched) =>
        "The assemblies Reach read were not compiled from the source in the working tree, so "
        + "any selection over them would describe code that is not there. "
        + $"{mismatched.Count} source file(s) differ from what the compiler recorded:\n"
        + string.Join("\n", mismatched.Select(verdict => "  " + verdict.Document.Path).Order(StringComparer.Ordinal))
        + "\n\nBuild, or drop --no-build.";

    private static IReadOnlyList<Notice> Notices(IReadOnlyList<DocumentVerdict> verdicts) =>
        [.. new[] { InvisibleNotice(verdicts), UnmappedRootNotice(verdicts) }.OfType<Notice>()];

    /// <summary>
    /// Documents recorded under a deterministic root Reach could not map. Never guessed: the
    /// symbols do not say which directory <c>/_1/</c> was, and a guess would turn a disclosed
    /// gap into a silent one.
    /// </summary>
    private static Notice? UnmappedRootNotice(IReadOnlyList<DocumentVerdict> verdicts)
    {
        var unmapped = verdicts
            .Where(verdict => verdict.Document.UnmappedRoot is not null)
            .ToArray();

        if (unmapped.Length == 0)
        {
            return null;
        }

        var roots = unmapped
            .Select(verdict => verdict.Document.UnmappedRoot!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new Notice(
            NoticeCodes.UnmappedDeterministicSourceRoot,
            // A blind spot: a change to one of these files can be neither verified nor joined.
            NoticeKind.BlindSpot,
            $"{unmapped.Length} compiled source file(s) were recorded under a deterministic source root "
            + $"({string.Join(", ", roots)}) that Reach cannot map to a directory, so they were not "
            + "checked against the working tree and a change to one cannot select a test. /_/ is read "
            + "as the repository root, and other roots only where the file is in the NuGet global "
            + "packages folder. Build with -p:DeterministicSourcePaths=false to record real paths.",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["roots"] = roots });
    }

    private static Notice? InvisibleNotice(IReadOnlyList<DocumentVerdict> verdicts)
    {
        var invisible = verdicts
            .Where(verdict => verdict.Class == DocumentClass.Invisible)
            .Select(verdict => verdict.Document.Path)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (invisible.Length == 0)
        {
            return null;
        }

        return new Notice(
            NoticeCodes.IgnoredUntrackedAssembly,
            // A blind spot rather than a widening: Reach can see the file, but it cannot
            // see changes to it, because git will never report one.
            NoticeKind.BlindSpot,
            $"{invisible.Length} compiled source file(s) are untracked and git-ignored, so "
            + "a change to one is invisible to Reach and cannot select a test: "
            + string.Join(", ", invisible),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["paths"] = invisible });
    }
}

/// <summary>
/// How one recorded document compared against the working tree.
/// </summary>
public enum DocumentClass
{
    /// <summary>
    /// On disk, and the file's bytes hash to the checksum the compiler recorded.
    /// </summary>
    Verified,

    /// <summary>
    /// On disk, and they do not. The assembly was compiled from different source.
    /// </summary>
    Mismatched,

    /// <summary>
    /// No file on disk: a source generator's output, or the SDK's own generated members.
    /// </summary>
    Generated,

    /// <summary>
    /// On disk, but the symbols recorded no checksum to compare against.
    /// </summary>
    Unverifiable,

    /// <summary>
    /// On disk, untracked <em>and</em> git-ignored. Compiled source Reach cannot see changes
    /// to, because <c>.gitignore</c> governs only untracked files. Reported, never selected on.
    /// </summary>
    Invisible,
}

public sealed record DocumentVerdict(SourceDocument Document, DocumentClass Class);

/// <summary>
/// What the run concluded about the correspondence between source and binaries.
/// </summary>
public enum CorrespondenceVerdict
{
    /// <summary>
    /// At least one document was checked, and every checked document agreed.
    /// </summary>
    Verified,

    /// <summary>
    /// Nothing could be checked — no symbols, or no checksums in them.
    /// </summary>
    Skipped,

    /// <summary>
    /// At least one document disagreed. Exit 5.
    /// </summary>
    Failed,
}

public sealed record CorrespondenceResult(
    CorrespondenceVerdict Verdict,
    string Message,
    IReadOnlyList<Notice> Notices,
    IReadOnlyList<DocumentVerdict> Documents)
{
    internal ExitCode ExitCode =>
        Verdict == CorrespondenceVerdict.Failed ? ExitCode.CorrespondenceFailed : ExitCode.Success;
}
