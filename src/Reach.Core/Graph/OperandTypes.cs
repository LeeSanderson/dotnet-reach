using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Reach.Graph;

/// <summary>
/// The type names one method's metadata gives the values on its evaluation stack: its arguments
/// and locals, and what each token-carrying instruction loads, creates or returns. Every name is
/// the one the type hierarchy is keyed on; anything that cannot be named reads as null, which
/// falls to the token.
/// </summary>
internal sealed class OperandTypes(MetadataReader reader)
{
    private readonly SignatureNames _names = new();

    /// <summary>
    /// Each argument's type, <c>this</c> first for an instance method.
    /// </summary>
    internal IReadOnlyList<string?> ArgumentsOf(MethodDefinition method)
    {
        var types = new List<string?>();

        if (!method.Attributes.HasFlag(MethodAttributes.Static))
        {
            types.Add(MetadataNames.FullNameOf(reader, method.GetDeclaringType()));
        }

        try
        {
            types.AddRange(method.DecodeSignature(_names, genericContext: null).ParameterTypes.Select(Clean));
        }
        catch (BadImageFormatException)
        {
            // Nothing to add; every argument then reads as unknown, which falls to the token.
        }

        return types;
    }

    internal IReadOnlyList<string?> LocalsOf(MethodBodyBlock body)
    {
        if (body.LocalSignature.IsNil)
        {
            return [];
        }

        try
        {
            return
            [
                .. reader.GetStandaloneSignature(body.LocalSignature)
                    .DecodeLocalSignature(_names, genericContext: null)
                    .Select(Clean)
            ];
        }
        catch (BadImageFormatException)
        {
            return [];
        }
    }

    /// <summary>
    /// The type a type token names.
    /// </summary>
    internal string? TypeNamed(int token)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)handle),
                HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)handle),
                HandleKind.TypeSpecification => Clean(
                    reader.GetTypeSpecification((TypeSpecificationHandle)handle)
                        .DecodeSignature(_names, genericContext: null)),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The type that declares the method a method token names.
    /// </summary>
    internal string? DeclaringTypeOf(int token)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.MethodDefinition => MetadataNames.FullNameOf(
                    reader,
                    reader.GetMethodDefinition((MethodDefinitionHandle)handle).GetDeclaringType()),
                HandleKind.MemberReference => ParentTypeOf(
                    reader.GetMemberReference((MemberReferenceHandle)handle).Parent),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// The type of the field a field token names.
    /// </summary>
    internal string? FieldTypeOf(int token)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);

            return handle.Kind switch
            {
                HandleKind.FieldDefinition => Clean(
                    reader.GetFieldDefinition((FieldDefinitionHandle)handle).DecodeSignature(_names, null)),
                HandleKind.MemberReference => Clean(
                    reader.GetMemberReference((MemberReferenceHandle)handle).DecodeFieldSignature(_names, null)),
                _ => null,
            };
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// What a call to the method a method token names takes off the stack and puts back: its
    /// parameter count, whether it also takes a receiver, and its return type (null for void).
    /// </summary>
    internal (int Parameters, bool IsInstance, string? Returns) CallShape(int token)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);

            switch (handle.Kind)
            {
                case HandleKind.MethodDefinition:
                {
                    var method = reader.GetMethodDefinition((MethodDefinitionHandle)handle);
                    var signature = method.DecodeSignature(_names, genericContext: null);

                    return (
                        signature.ParameterTypes.Length,
                        !method.Attributes.HasFlag(MethodAttributes.Static),
                        Returns(signature));
                }

                case HandleKind.MemberReference:
                {
                    var reference = reader.GetMemberReference((MemberReferenceHandle)handle);
                    var signature = reference.DecodeMethodSignature(_names, genericContext: null);

                    return (
                        signature.ParameterTypes.Length,
                        signature.Header.IsInstance,
                        Returns(signature));
                }

                case HandleKind.MethodSpecification:
                    return CallShape(
                        MetadataTokens.GetToken(
                            reader.GetMethodSpecification((MethodSpecificationHandle)handle).Method));

                default:
                    return (0, false, null);
            }
        }
        catch (BadImageFormatException)
        {
            return (0, false, null);
        }
    }

    /// <summary>
    /// Drops the by-ref marker and any instantiation, leaving the name the hierarchy is keyed on.
    /// </summary>
    private static string? Clean(string type)
    {
        var name = MetadataNames.WithoutInstantiation(type.TrimEnd('&'));

        return name.Length == 0 || name.StartsWith('!') ? null : name;
    }

    private static string? Returns(MethodSignature<string> signature) =>
        signature.ReturnType == "System.Void" ? null : Clean(signature.ReturnType) ?? "?";

    private string? ParentTypeOf(EntityHandle parent) =>
        parent.Kind switch
        {
            HandleKind.TypeDefinition => MetadataNames.FullNameOf(reader, (TypeDefinitionHandle)parent),
            HandleKind.TypeReference => MetadataNames.FullNameOf(reader, (TypeReferenceHandle)parent),
            HandleKind.TypeSpecification => Clean(
                reader.GetTypeSpecification((TypeSpecificationHandle)parent)
                    .DecodeSignature(_names, genericContext: null)),
            _ => null,
        };
}
