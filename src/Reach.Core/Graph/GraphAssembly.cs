using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Reach.Assemblies;

namespace Reach.Graph;

/// <summary>
/// One assembly instance as the graph pass sees it: an ordinal, an already-opened reader, and
/// the index that turns a canonical signature into a <see cref="MethodId"/>.
/// </summary>
/// <remarks>
/// The reader is a parameter, not a port. The BCL type already <em>is</em> the seam, so an
/// in-memory Roslyn compilation emitted to a <c>MemoryStream</c> and a file on disk are the
/// same type — and an <c>IAssemblyReader</c> wrapping it would be an interface nearly as
/// complex as its implementation.
/// </remarks>
internal sealed class GraphAssembly
{
    private Dictionary<string, List<MethodId>>? index;

    internal GraphAssembly(
        int ordinal,
        string name,
        TargetFrameworkMoniker? framework,
        MetadataReader reader,
        PEReader? peReader)
    {
        Ordinal = ordinal;
        Name = name;
        Framework = framework;
        Reader = reader;
        PEReader = peReader;
    }

    internal int Ordinal { get; }

    internal string Name { get; }

    internal TargetFrameworkMoniker? Framework { get; }

    internal MetadataReader Reader { get; }

    /// <summary>Null when the metadata came from somewhere other than a PE file.</summary>
    internal PEReader? PEReader { get; }

    /// <summary>
    /// Canonical signature to the methods carrying it, built once at load in O(methods) and
    /// then one lookup per call site — O(call sites), not the accidentally-quadratic shape.
    /// </summary>
    /// <remarks>
    /// A list rather than a single id, because varargs, <c>modopt</c>/<c>modreq</c> and
    /// function-pointer parameters leave residual ambiguity, and the safe answer there is to
    /// edge to every candidate.
    /// </remarks>
    internal IReadOnlyDictionary<string, List<MethodId>> Index => index ??= BuildIndex();

    private Dictionary<string, List<MethodId>> BuildIndex()
    {
        var built = new Dictionary<string, List<MethodId>>(StringComparer.Ordinal);
        var names = new SignatureNames();

        foreach (var handle in Reader.MethodDefinitions)
        {
            var method = Reader.GetMethodDefinition(handle);
            var key = KeyOf(method, names);

            if (key is null)
            {
                continue;
            }

            if (!built.TryGetValue(key, out var candidates))
            {
                built[key] = candidates = [];
            }

            candidates.Add(MethodId.Definition(Ordinal, MetadataTokens.GetToken(handle)));
        }

        return built;
    }

    private string? KeyOf(MethodDefinition method, SignatureNames names)
    {
        try
        {
            var declaringType = MetadataNames.FullNameOf(Reader, method.GetDeclaringType());
            var signature = method.DecodeSignature(names, genericContext: null);

            return declaringType
                + "::"
                + MetadataNames.Key(
                    Reader.GetString(method.Name),
                    MetadataNames.GenericArityOf(Reader, method),
                    signature);
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }
}
