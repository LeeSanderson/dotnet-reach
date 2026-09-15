using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;

namespace Reach.Graph;

/// <summary>
/// Decodes a metadata signature into a type-name string.
/// </summary>
/// <remarks>
/// PRD §9.4 forbids strings in <em>identity</em>, not in a build-once lookup. This is that
/// lookup: it removes the whole overload-ambiguity class a name-plus-parameter-count key would
/// leave behind, and the strings it produces never reach <see cref="MethodId"/>.
/// </remarks>
internal sealed class SignatureNames : ISignatureTypeProvider<string, object?>
{
    /// <summary>
    /// The first type handle the decoder reached. A member reference against a generic type
    /// instance names its parent by a <c>TypeSpecification</c>, which carries no assembly of
    /// its own — this is how the assembly behind one is recovered.
    /// </summary>
    internal EntityHandle FirstType { get; private set; }

    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
    {
        PrimitiveTypeCode.Boolean => "System.Boolean",
        PrimitiveTypeCode.Byte => "System.Byte",
        PrimitiveTypeCode.Char => "System.Char",
        PrimitiveTypeCode.Double => "System.Double",
        PrimitiveTypeCode.Int16 => "System.Int16",
        PrimitiveTypeCode.Int32 => "System.Int32",
        PrimitiveTypeCode.Int64 => "System.Int64",
        PrimitiveTypeCode.IntPtr => "System.IntPtr",
        PrimitiveTypeCode.Object => "System.Object",
        PrimitiveTypeCode.SByte => "System.SByte",
        PrimitiveTypeCode.Single => "System.Single",
        PrimitiveTypeCode.String => "System.String",
        PrimitiveTypeCode.TypedReference => "System.TypedReference",
        PrimitiveTypeCode.UInt16 => "System.UInt16",
        PrimitiveTypeCode.UInt32 => "System.UInt32",
        PrimitiveTypeCode.UInt64 => "System.UInt64",
        PrimitiveTypeCode.UIntPtr => "System.UIntPtr",
        PrimitiveTypeCode.Void => "System.Void",
        _ => "?",
    };

    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
    {
        Remember(handle);
        return MetadataNames.FullNameOf(reader, handle);
    }

    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
    {
        Remember(handle);
        return MetadataNames.FullNameOf(reader, handle);
    }

    public string GetTypeFromSpecification(
        MetadataReader reader,
        object? genericContext,
        TypeSpecificationHandle handle,
        byte rawTypeKind) =>
        reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

    public string GetSZArrayType(string elementType) => elementType + "[]";

    public string GetArrayType(string elementType, ArrayShape shape) =>
        elementType + "[" + new string(',', Math.Max(shape.Rank - 1, 0)) + "]";

    public string GetPointerType(string elementType) => elementType + "*";

    public string GetByReferenceType(string elementType) => elementType + "&";

    public string GetPinnedType(string elementType) => elementType;

    /// <summary>
    /// Kept with its arguments, because <c>void M(List&lt;int&gt;)</c> and
    /// <c>void M(List&lt;string&gt;)</c> are two overloads. Identity collapses generic
    /// instantiations; a <em>signature</em> cannot.
    /// </summary>
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
        genericType + "<" + string.Join(",", typeArguments) + ">";

    public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index;

    public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index;

    /// <summary>
    /// Dropped. <c>modopt</c> and <c>modreq</c> are exactly the residual ambiguity the design
    /// expects to hit, and edging to every candidate is the safe direction.
    /// </summary>
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

    public string GetFunctionPointerType(MethodSignature<string> signature) =>
        "delegate*<" + string.Join(",", signature.ParameterTypes.Append(signature.ReturnType)) + ">";

    public string GetTypeFromSerializedName(string name) => name;

    public PrimitiveTypeCode GetUnderlyingEnumType(string type) => PrimitiveTypeCode.Int32;

    public bool IsSystemType(string type) => type == "System.Type";

    private void Remember(EntityHandle handle)
    {
        if (FirstType.IsNil)
        {
            FirstType = handle;
        }
    }
}

/// <summary>Full names for types and the canonical key a method is looked up by.</summary>
internal static class MetadataNames
{
    /// <summary>
    /// The canonical signature of a method: everything that distinguishes one overload from
    /// another, and nothing that does not.
    /// </summary>
    /// <remarks>
    /// The return type is part of it, because conversion operators differ only by return type.
    /// </remarks>
    internal static string Key(string name, int genericArity, MethodSignature<string> signature)
    {
        var builder = new StringBuilder(name);

        if (genericArity > 0)
        {
            builder.Append('`').Append(genericArity);
        }

        builder.Append('(').AppendJoin(',', signature.ParameterTypes).Append(')');
        builder.Append(':').Append(signature.ReturnType);

        return builder.ToString();
    }

    internal static string FullNameOf(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var definition = reader.GetTypeDefinition(handle);
        var name = reader.GetString(definition.Name);

        if (definition.IsNested)
        {
            return FullNameOf(reader, definition.GetDeclaringType()) + "+" + name;
        }

        var containing = reader.GetString(definition.Namespace);

        return containing.Length == 0 ? name : containing + "." + name;
    }

    internal static string FullNameOf(MetadataReader reader, TypeReferenceHandle handle)
    {
        var reference = reader.GetTypeReference(handle);
        var name = reader.GetString(reference.Name);

        if (reference.ResolutionScope.Kind == HandleKind.TypeReference)
        {
            return FullNameOf(reader, (TypeReferenceHandle)reference.ResolutionScope) + "+" + name;
        }

        var containing = reader.GetString(reference.Namespace);

        return containing.Length == 0 ? name : containing + "." + name;
    }

    /// <summary>The name of the assembly a type reference resolves into, or null when it is this one.</summary>
    internal static string? AssemblyOf(MetadataReader reader, TypeReferenceHandle handle)
    {
        var scope = reader.GetTypeReference(handle).ResolutionScope;

        return scope.Kind switch
        {
            HandleKind.AssemblyReference =>
                reader.GetString(reader.GetAssemblyReference((AssemblyReferenceHandle)scope).Name),
            HandleKind.TypeReference => AssemblyOf(reader, (TypeReferenceHandle)scope),
            _ => null,
        };
    }

    /// <summary>
    /// The generic definition behind a decoded type name: <c>List`1&lt;System.Int32&gt;</c>
    /// becomes <c>List`1</c>. A member reference against a generic instance carries the
    /// definition's own signature, so this is the name to look it up under.
    /// </summary>
    internal static string WithoutInstantiation(string typeName)
    {
        var index = typeName.IndexOf('<', StringComparison.Ordinal);

        return index < 0 ? typeName : typeName[..index];
    }

    /// <summary>How many generic parameters a method declares. Part of what tells overloads apart.</summary>
    internal static int GenericArityOf(MetadataReader reader, MethodDefinition method) =>
        method.GetGenericParameters().Count;

    internal static bool IsStatic(MethodDefinition method) =>
        (method.Attributes & MethodAttributes.Static) != 0;
}
