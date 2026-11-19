using System.Reflection.Metadata;
using Reach.Graph;
using static Reach.Buckets;

namespace Reach.Selection;

/// <summary>
/// Every type definition in the analysis scope, by full name, so a base class or fixture named
/// in one first-party assembly can be found in another.
/// </summary>
internal sealed class FirstPartyTypes
{
    private readonly Dictionary<string, List<FirstPartyType>> _byName = new(StringComparer.Ordinal);

    internal FirstPartyTypes(IEnumerable<GraphAssembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            foreach (var handle in assembly.Reader.TypeDefinitions)
            {
                Bucket(_byName, MetadataNames.FullNameOf(assembly.Reader, handle))
                    .Add(new FirstPartyType(assembly, handle));
            }
        }
    }

    /// <summary>
    /// The first-party definitions a type handle in <paramref name="from"/> names. None for a
    /// nil handle or a type outside the scope.
    /// </summary>
    internal IEnumerable<FirstPartyType> Resolve(GraphAssembly from, EntityHandle handle)
    {
        if (handle.IsNil)
        {
            return [];
        }

        var reader = from.Reader;

        return handle.Kind switch
        {
            HandleKind.TypeDefinition => [new FirstPartyType(from, (TypeDefinitionHandle)handle)],
            HandleKind.TypeReference => Named(from, MetadataNames.FullNameOf(reader, (TypeReferenceHandle)handle)),
            HandleKind.TypeSpecification => Named(
                from,
                MetadataNames.WithoutInstantiation(reader
                    .GetTypeSpecification((TypeSpecificationHandle)handle)
                    .DecodeSignature(new SignatureNames(), genericContext: null))),
            _ => [],
        };
    }

    /// <summary>
    /// The definitions of <paramref name="name"/> as <paramref name="from"/> would see them: its
    /// own if it declares one, otherwise the instances built for the same target framework.
    /// </summary>
    /// <remarks>
    /// A multi-targeted project contributes one instance per framework, and a <c>net8.0</c>
    /// assembly references the <c>net8.0</c> build. Where no instance matches — a
    /// <c>netstandard</c> library — every one is taken, which is the safe direction.
    /// </remarks>
    internal IEnumerable<FirstPartyType> Named(GraphAssembly from, string name)
    {
        if (!_byName.TryGetValue(name, out var candidates))
        {
            return [];
        }

        var own = candidates.Where(candidate => candidate.Assembly == from).ToList();

        if (own.Count > 0)
        {
            return own;
        }

        var matching = candidates
            .Where(candidate =>
                candidate.Assembly.Framework is not null
                && from.Framework is not null
                && candidate.Assembly.Framework.Matches(from.Framework))
            .ToList();

        return matching.Count > 0 ? matching : candidates;
    }
}

/// <summary>
/// One type definition and the assembly instance it is in.
/// </summary>
/// <param name="Assembly">The assembly instance.</param>
/// <param name="Type">The definition within it.</param>
internal readonly record struct FirstPartyType(GraphAssembly Assembly, TypeDefinitionHandle Type);
