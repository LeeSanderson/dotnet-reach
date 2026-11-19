using System.Reflection.Metadata;

namespace Reach.Graph;

/// <summary>
/// Recovers the static type a dispatch site can be shown to hold, by a single-pass abstract
/// interpretation of the evaluation stack tracking type names only.
/// </summary>
/// <remarks>
/// <para>
/// A future reader will reasonably ask why there is an abstract interpreter inside what should
/// be a metadata reader. This is why: <strong>Roslyn emits <c>callvirt</c> against the
/// slot-defining declaration</strong>, not the most-derived statically-known type. Verified by
/// disassembly rather than assumed — <c>x.ToString()</c> where <c>x</c> is statically a type
/// that declares an override still emits <c>callvirt System.Object::ToString()</c>, and
/// <c>using var r = new Res()</c> on a <em>sealed</em> class implementing <c>IDisposable</c>
/// emits <c>callvirt System.IDisposable::Dispose()</c> rather than a direct call.
/// </para>
/// <para>
/// Taking the token at face value makes fan-out maximal: every <c>ToString()</c> call site
/// edges to one <c>object::ToString</c> node, which widens to every override anywhere, so
/// changing any one override reverse-reaches almost the entire suite. That is not conservatism,
/// it is a graph with no information in it.
/// </para>
/// <para>
/// <strong>The stack is cleared at every branch target.</strong> A receiver pushed across a
/// branch boundary therefore reads as unknown and falls to the token — which is exactly the
/// stack-merge residue the design accepts: a ternary compiling to branches pushing different
/// types keeps full fan-out at that one site. Errs over.
/// </para>
/// </remarks>
internal sealed class ReceiverTypes
{
    /// <summary>
    /// <c>ldarg</c> and <c>ldloc</c> each have short forms for the first four slots.
    /// </summary>
    private const int ShortFormSlots = 4;

    /// <summary>
    /// The instructions the interpretation knows more about than their stack behaviour, by the
    /// family each one belongs to. Everything else only pops and pushes unknowns.
    /// </summary>
    private static readonly Dictionary<ILOpCode, Family> Families = new()
    {
        [ILOpCode.Newobj] = Family.Construction,

        [ILOpCode.Castclass] = Family.Conversion,
        [ILOpCode.Isinst] = Family.Conversion,
        [ILOpCode.Unbox_any] = Family.Conversion,
        [ILOpCode.Box] = Family.Conversion,

        [ILOpCode.Ldarg_0] = Family.ArgumentLoad,
        [ILOpCode.Ldarg_1] = Family.ArgumentLoad,
        [ILOpCode.Ldarg_2] = Family.ArgumentLoad,
        [ILOpCode.Ldarg_3] = Family.ArgumentLoad,
        [ILOpCode.Ldarg] = Family.ArgumentLoad,
        [ILOpCode.Ldarg_s] = Family.ArgumentLoad,

        [ILOpCode.Ldloc_0] = Family.LocalLoad,
        [ILOpCode.Ldloc_1] = Family.LocalLoad,
        [ILOpCode.Ldloc_2] = Family.LocalLoad,
        [ILOpCode.Ldloc_3] = Family.LocalLoad,
        [ILOpCode.Ldloc] = Family.LocalLoad,
        [ILOpCode.Ldloc_s] = Family.LocalLoad,

        [ILOpCode.Ldsfld] = Family.FieldLoad,
        [ILOpCode.Ldfld] = Family.FieldLoad,

        [ILOpCode.Dup] = Family.Duplication,

        [ILOpCode.Call] = Family.Call,
        [ILOpCode.Callvirt] = Family.Call,

        [ILOpCode.Ldftn] = Family.FunctionLoad,
        [ILOpCode.Ldvirtftn] = Family.FunctionLoad,
    };

    private readonly OperandTypes _types;
    private readonly IReadOnlyList<string?> _arguments;
    private readonly IReadOnlyList<string?> _locals;
    private readonly List<string?> _stack = [];
    private readonly Dictionary<int, string> _inferred = [];

    private ReceiverTypes(MetadataReader reader, MethodDefinition method, MethodBodyBlock body)
    {
        _types = new OperandTypes(reader);
        _arguments = _types.ArgumentsOf(method);
        _locals = _types.LocalsOf(body);
    }

    private enum Family
    {
        /// <summary>
        /// Anything that only pops and pushes unknowns.
        /// </summary>
        Other,

        // Rung 1: an exact type.
        Construction,
        Conversion,

        // Rung 2: a signature type.
        ArgumentLoad,
        LocalLoad,
        FieldLoad,

        Duplication,
        Call,
        FunctionLoad,
    }

    /// <summary>
    /// The inferred receiver type at each call instruction's offset, or absent where the ladder
    /// falls through to the token.
    /// </summary>
    internal static IReadOnlyDictionary<int, string> Infer(
        GraphAssembly assembly,
        MethodDefinition method,
        MethodBodyBlock body,
        ReadOnlySpan<byte> il)
    {
        var interpretation = new ReceiverTypes(assembly.Reader, method, body);
        var boundaries = ILDecoder.Boundaries(il, body);
        var offset = 0;

        // An instruction Read cannot decode is a nop with a negative next offset: the walk ends.
        while (offset >= 0 && offset < il.Length)
        {
            if (boundaries.Contains(offset))
            {
                // Everything before a branch target reached it from somewhere this pass did not
                // follow, so nothing on the stack can be trusted across it.
                interpretation._stack.Clear();
            }

            var (opCode, operand, next) = ILDecoder.Read(il, offset);

            interpretation.Apply(offset, opCode, operand);

            offset = next;
        }

        return interpretation._inferred;
    }

    private void Apply(int offset, ILOpCode opCode, int operand)
    {
        switch (Families.GetValueOrDefault(opCode))
        {
            case Family.Construction:
                Pop(_types.CallShape(operand).Parameters);
                _stack.Add(_types.DeclaringTypeOf(operand));
                break;

            case Family.Conversion:
                Pop(1);
                _stack.Add(_types.TypeNamed(operand));
                break;

            case Family.ArgumentLoad:
                _stack.Add(At(_arguments, Slot(opCode, ILOpCode.Ldarg_0, operand)));
                break;

            case Family.LocalLoad:
                _stack.Add(At(_locals, Slot(opCode, ILOpCode.Ldloc_0, operand)));
                break;

            case Family.FieldLoad:
                LoadField(opCode, operand);
                break;

            case Family.Duplication:
                _stack.Add(Peek(0));
                break;

            case Family.Call:
                Call(offset, operand);
                break;

            case Family.FunctionLoad:
                LoadFunction(offset, opCode);
                break;

            default:
                ApplyStackEffect(opCode);
                break;
        }
    }

    /// <summary>
    /// <c>ldsfld</c> pushes the field's type; <c>ldfld</c> first pops the object it reads from.
    /// </summary>
    private void LoadField(ILOpCode opCode, int operand)
    {
        if (opCode == ILOpCode.Ldfld)
        {
            Pop(1);
        }

        _stack.Add(_types.FieldTypeOf(operand));
    }

    /// <summary>
    /// The receiver sits below the arguments, so it is read before they are popped.
    /// </summary>
    private void Call(int offset, int operand)
    {
        var (parameters, isInstance, returns) = _types.CallShape(operand);
        var receiver = Peek(parameters);

        if (isInstance && receiver is not null)
        {
            _inferred[offset] = receiver;
        }

        Pop(parameters + (isInstance ? 1 : 0));

        if (returns is not null)
        {
            _stack.Add(returns);
        }
    }

    /// <summary>
    /// <c>ldvirtftn</c> consumes its receiver; <c>ldftn</c> does not.
    /// </summary>
    private void LoadFunction(int offset, ILOpCode opCode)
    {
        if (opCode == ILOpCode.Ldvirtftn)
        {
            if (Peek(0) is { } receiver)
            {
                _inferred[offset] = receiver;
            }

            Pop(1);
        }

        _stack.Add(null);
    }

    private void ApplyStackEffect(ILOpCode opCode)
    {
        var (pops, pushes) = ILDecoder.StackEffectOf(opCode);

        Pop(pops);

        for (var push = 0; push < pushes; push++)
        {
            _stack.Add(null);
        }
    }

    private void Pop(int count)
    {
        for (var index = 0; index < count && _stack.Count > 0; index++)
        {
            _stack.RemoveAt(_stack.Count - 1);
        }
    }

    /// <summary>
    /// The slot <paramref name="below"/> places from the top, or null when the stack is short.
    /// </summary>
    private string? Peek(int below) =>
        _stack.Count > below ? _stack[^(below + 1)] : null;

    /// <summary>
    /// The variable an argument or local load names. The short forms are numbered
    /// consecutively, so their slot is the distance from the <c>_0</c> form; the others carry it
    /// as their operand.
    /// </summary>
    private static int Slot(ILOpCode opCode, ILOpCode shortForm0, int operand)
    {
        var distance = opCode - shortForm0;

        return distance is >= 0 and < ShortFormSlots ? distance : operand;
    }

    private static string? At(IReadOnlyList<string?> values, int index) =>
        index >= 0 && index < values.Count ? values[index] : null;
}
