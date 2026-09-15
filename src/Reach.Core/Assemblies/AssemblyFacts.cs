using System.Reflection.Metadata;

namespace Reach.Assemblies;

/// <summary>
/// Facts read from an assembly's own metadata.
/// </summary>
/// <remarks>
/// Every function here takes an already-opened <see cref="MetadataReader"/>. That is the whole
/// seam: an in-memory Roslyn compilation emitted to a <c>MemoryStream</c> and a file on disk
/// are the same type, so there is nothing to fake. An <c>IAssemblyReader</c> wrapping this
/// would be an interface nearly as complex as its implementation.
/// </remarks>
internal static class AssemblyFacts
{
    private const string VersioningNamespace = "System.Runtime.Versioning";

    /// <summary>The assembly's simple name, as metadata records it rather than as the file is spelled.</summary>
    internal static string SimpleName(MetadataReader reader) =>
        reader.GetString(reader.GetAssemblyDefinition().Name);

    /// <summary>
    /// The module version id: a fresh GUID per compilation. Two files carrying the same MVID
    /// are the same build of the same assembly, which is how a copy in a consuming project's
    /// output is told from a genuinely second candidate.
    /// </summary>
    internal static Guid ModuleVersionId(MetadataReader reader) =>
        reader.GetGuid(reader.GetModuleDefinition().Mvid);

    /// <summary>
    /// The framework name from <c>TargetFrameworkAttribute</c> — <c>.NETCoreApp,Version=v10.0</c>.
    /// Read from the assembly itself, never from a path segment: the target framework is
    /// exactly what every ambiguous layout destroys on disk.
    /// </summary>
    internal static string? TargetFramework(MetadataReader reader) =>
        AssemblyAttributeArgument(reader, "TargetFrameworkAttribute");

    /// <summary>
    /// The platform from <c>TargetPlatformAttribute</c> — <c>Windows7.0</c>.
    /// </summary>
    /// <remarks>
    /// Read for a reason beyond completeness: it separates a platform-suffixed instance from a
    /// plain one, so <c>net10.0</c> and <c>net10.0-windows</c> resolve as two distinct assembly
    /// instances and the ambiguity error does not misfire. What it cannot do is recover the
    /// <em>declared</em> moniker string — it always carries a version, so <c>net10.0-windows</c>
    /// reads back as <c>Windows7.0</c> and <c>net10.0-windows7.0</c> is byte-identical.
    /// </remarks>
    internal static string? TargetPlatform(MetadataReader reader) =>
        AssemblyAttributeArgument(reader, "TargetPlatformAttribute");

    private static string? AssemblyAttributeArgument(MetadataReader reader, string attributeName)
    {
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);

            if (!IsNamed(reader, attribute, VersioningNamespace, attributeName))
            {
                continue;
            }

            var blob = reader.GetBlobReader(attribute.Value);

            // Prolog, then one SerString. Both attributes have a single string constructor,
            // so there is nothing here worth a full attribute decoder.
            if (blob.Length < 2 || blob.ReadUInt16() != 1)
            {
                return null;
            }

            return blob.ReadSerializedString();
        }

        return null;
    }

    private static bool IsNamed(
        MetadataReader reader,
        CustomAttribute attribute,
        string attributeNamespace,
        string attributeName)
    {
        var (found, foundNamespace) = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => TypeOf(
                reader,
                reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent),
            HandleKind.MethodDefinition => TypeOf(
                reader,
                reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType()),
            _ => (null, null),
        };

        return found == attributeName && foundNamespace == attributeNamespace;
    }

    private static (string? Name, string? Namespace) TypeOf(MetadataReader reader, EntityHandle handle) =>
        handle.Kind switch
        {
            HandleKind.TypeReference => Named(reader, reader.GetTypeReference((TypeReferenceHandle)handle)),
            HandleKind.TypeDefinition => Named(reader, reader.GetTypeDefinition((TypeDefinitionHandle)handle)),
            _ => (null, null),
        };

    private static (string? Name, string? Namespace) Named(MetadataReader reader, TypeReference type) =>
        (reader.GetString(type.Name), reader.GetString(type.Namespace));

    private static (string? Name, string? Namespace) Named(MetadataReader reader, TypeDefinition type) =>
        (reader.GetString(type.Name), reader.GetString(type.Namespace));
}
