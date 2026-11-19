using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Reach.Graph;

namespace Reach.Selection;

/// <summary>
/// Every test method in one test assembly instance — not only the selected ones, because the
/// totals, the rendered-match computation and the over-selection measurement all read the
/// whole list.
/// </summary>
internal static class TestMethods
{
    internal static IReadOnlyList<TestMethod> In(GraphAssembly assembly, TestFramework framework)
    {
        var reader = assembly.Reader;
        var attributes = MarkerTypesIn(reader, framework);
        var found = new List<TestMethod>();

        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);

            if (!IsMarked(reader, method, attributes))
            {
                continue;
            }

            found.Add(new TestMethod(
                MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(handle)),
                MetadataNames.FullNameOf(reader, method.GetDeclaringType()).Replace('+', '.'),
                reader.GetString(method.Name)));
        }

        return [.. found.OrderBy(test => test.FullyQualifiedName, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The attribute type names that mark a test, including any type in this assembly that
    /// derives from one of them.
    /// </summary>
    /// <remarks>
    /// A custom <c>[IntegrationFact] : FactAttribute</c> is ordinary in real suites, and
    /// missing it would drop every test using it — under-selection. Only first-party
    /// derivations are followed, because anything else needs the defining assembly, which is
    /// outside the analysis scope.
    /// </remarks>
    private static HashSet<string> MarkerTypesIn(MetadataReader reader, TestFramework framework)
    {
        var markers = new HashSet<string>(framework.TestAttributes, StringComparer.Ordinal);

        // Repeated until nothing new is found, so a two-step derivation is covered too.
        for (var added = true; added;)
        {
            added = false;

            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);
                var baseName = NameOf(reader, type.BaseType);

                if (baseName is not null
                    && markers.Contains(baseName)
                    && markers.Add(MetadataNames.FullNameOf(reader, handle)))
                {
                    added = true;
                }
            }
        }

        return markers;
    }

    /// <summary>
    /// The full name of each attribute in <paramref name="attributes"/> that names its type.
    /// </summary>
    internal static IEnumerable<string> AttributeNames(MetadataReader reader, IEnumerable<CustomAttributeHandle> attributes)
    {
        foreach (var handle in attributes)
        {
            var attribute = reader.GetCustomAttribute(handle);

            if (NameOf(reader, DeclaringTypeOf(reader, attribute.Constructor)) is { } name)
            {
                yield return name;
            }
        }
    }

    private static bool IsMarked(MetadataReader reader, MethodDefinition method, HashSet<string> markers) =>
        AttributeNames(reader, method.GetCustomAttributes()).Any(markers.Contains);

    private static EntityHandle DeclaringTypeOf(MetadataReader reader, EntityHandle constructor) =>
        constructor.Kind switch
        {
            HandleKind.MemberReference =>
                reader.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition =>
                reader.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default,
        };

    private static string? NameOf(MetadataReader reader, EntityHandle type)
    {
        if (type.IsNil)
        {
            return null;
        }

        return type.Kind switch
        {
            HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)type),
            HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)type),
            _ => null,
        };
    }
}

/// <summary>
/// The unit of selection. Individual cases of a parameterised test are never selected
/// independently of the method that declares them.
/// </summary>
/// <param name="Id">The method's node in the call graph.</param>
/// <param name="DeclaringType">Fully qualified, as a filter expression will need it.</param>
/// <param name="Name">The method's name.</param>
public sealed record TestMethod(MethodId Id, string DeclaringType, string Name)
{
    internal string FullyQualifiedName => DeclaringType + "." + Name;

    public override string ToString() => FullyQualifiedName;
}
