using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Reach.Tests.Fixtures;

/// <summary>One assembly compiled from a source string, with its portable PDB.</summary>
/// <param name="Symbols">Empty when the symbols were embedded in <paramref name="Image"/>.</param>
internal sealed record CompiledAssembly(string Name, byte[] Image, byte[] Symbols)
{
    /// <summary>Writes the assembly and, unless they are embedded, its symbols.</summary>
    internal string WriteTo(string directory)
    {
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, Name + ".dll");

        File.WriteAllBytes(path, Image);

        if (Symbols.Length > 0)
        {
            File.WriteAllBytes(Path.ChangeExtension(path, ".pdb"), Symbols);
        }

        return path;
    }

    /// <summary>Opens the image the way production code does, without writing anything to disk.</summary>
    internal T Read<T>(Func<MetadataReader, T> read)
    {
        using var peReader = new PEReader(ImmutableArray.Create(Image));

        return read(peReader.GetMetadataReader());
    }
}

/// <summary>
/// Compiles C# to real IL in memory. This is what "anything testable in memory is tested in
/// memory" buys: an in-memory compilation and a file on disk produce the same
/// <see cref="MetadataReader"/>, so there is nothing to fake and every IL shape is a source
/// string away.
/// </summary>
internal static class Compiled
{
    /// <summary>What <see cref="File.WriteAllText(string, string?)"/> writes, byte for byte.</summary>
    private static readonly Encoding Utf8NoPreamble = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private static readonly Lazy<ImmutableArray<MetadataReference>> FrameworkReferences =
        new(() =>
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
        ]);

    /// <summary>
    /// Compiles one source file.
    /// </summary>
    /// <param name="documentPath">
    /// The path the debug symbols will record. What first-party classification reads, so tests
    /// can put an assembly's source inside or outside a working tree without moving a file.
    /// </param>
    /// <param name="targetFramework">Stamped as <c>TargetFrameworkAttribute</c>, as the SDK does.</param>
    /// <param name="targetPlatform">Stamped as <c>TargetPlatformAttribute</c> when given.</param>
    internal static CompiledAssembly Assembly(
        string name,
        string source,
        string documentPath,
        string targetFramework = ".NETCoreApp,Version=v10.0",
        string? targetPlatform = null,
        IEnumerable<CompiledAssembly>? references = null,
        bool embedSymbols = false) =>
        Assembly(name, [(source, documentPath)], targetFramework, targetPlatform, references, embedSymbols);

    /// <param name="embedSymbols">
    /// Put the portable PDB inside the image, as <c>&lt;DebugType&gt;embedded&lt;/DebugType&gt;</c>
    /// does. Both shapes have to verify identically.
    /// </param>
    internal static CompiledAssembly Assembly(
        string name,
        IEnumerable<(string Source, string DocumentPath)> files,
        string targetFramework = ".NETCoreApp,Version=v10.0",
        string? targetPlatform = null,
        IEnumerable<CompiledAssembly>? references = null,
        bool embedSymbols = false)
    {
        var options = new CSharpParseOptions(LanguageVersion.Preview);

        // The encoding is not optional: without it Roslyn refuses to emit debug information
        // (CS8055), because it cannot record a document checksum it cannot compute.
        //
        // And it must be UTF-8 *without* a byte-order mark. Roslyn hashes the encoding's
        // preamble along with the text, while File.WriteAllText writes none — so with the
        // Encoding.UTF8 singleton every document a fixture writes to disk would read as a
        // correspondence failure.
        var trees = files
            .Select(file => CSharpSyntaxTree.ParseText(
                SourceText.From(file.Source, Utf8NoPreamble),
                options,
                file.DocumentPath))
            .Append(CSharpSyntaxTree.ParseText(
                SourceText.From(Attributes(targetFramework, targetPlatform), Utf8NoPreamble),
                options))
            .ToArray();

        var compilation = CSharpCompilation.Create(
            name,
            trees,
            [
                .. FrameworkReferences.Value,
                .. (references ?? []).Select(reference =>
                    (MetadataReference)MetadataReference.CreateFromImage(reference.Image))
            ],
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                // Function pointers are one of the IL shapes the graph has to get right.
                allowUnsafe: true,
                // The tests emit types the framework also defines; nothing here is run.
                specificDiagnosticOptions: new Dictionary<string, ReportDiagnostic>
                {
                    ["CS0436"] = ReportDiagnostic.Suppress,
                }));

        using var image = new MemoryStream();
        using var symbols = new MemoryStream();

        var result = compilation.Emit(
            image,
            embedSymbols ? null : symbols,
            options: new EmitOptions(
                debugInformationFormat: embedSymbols
                    ? DebugInformationFormat.Embedded
                    : DebugInformationFormat.PortablePdb));

        if (!result.Success)
        {
            throw new InvalidOperationException(
                "The fixture did not compile:\n"
                + string.Join(
                    "\n",
                    result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        }

        return new CompiledAssembly(name, image.ToArray(), embedSymbols ? [] : symbols.ToArray());
    }

    private static string Attributes(string targetFramework, string? targetPlatform)
    {
        var platform = targetPlatform is null
            ? string.Empty
            : $"""[assembly: System.Runtime.Versioning.TargetPlatform("{targetPlatform}")]""";

        return $"""
            [assembly: System.Runtime.Versioning.TargetFramework("{targetFramework}")]
            {platform}
            """;
    }
}
