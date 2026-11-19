using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using Reach.Graph;

namespace Reach.Selection;

/// <summary>
/// Edges from each test method to what its framework runs around it: its class's constructors
/// and disposal, its setup and teardown, and the fixtures it is given.
/// </summary>
/// <remarks>
/// <para>
/// <strong>The same hole containment closes, one level up.</strong> A framework invokes a test
/// method by reflection, and constructs and disposes its class the same way, so no instruction
/// connects the test to its constructor. A change reached only from there — a field initialiser,
/// a fixture, a <c>[SetUp]</c> — would select nothing, silently.
/// </para>
/// <para>
/// The edges carry <see cref="EdgeProvenance.TestLifecycle"/>, which is synthesised: the
/// framework's contract makes the control flow certain. They are built from the recognition
/// table, so a framework's lifecycle is a row's data like its test attributes are. See ADR-0019.
/// </para>
/// </remarks>
internal static class TestLifecycle
{
    /// <summary>
    /// What every framework runs on an instance it creates and disposes. Field initialisers are
    /// compiled into the constructors, so they need no rule of their own.
    /// </summary>
    private static readonly string[] ConstructionAndDisposal = [".ctor", ".cctor", "Dispose", "DisposeAsync"];

    /// <summary>
    /// Every lifecycle edge in <paramref name="assemblies"/>, which are the whole analysis scope:
    /// a base class or fixture can live in a first-party assembly other than the test's own.
    /// </summary>
    internal static IReadOnlyList<Edge> EdgesIn(IReadOnlyList<GraphAssembly> assemblies)
    {
        var types = new FirstPartyTypes(assemblies);
        var edges = new List<Edge>();

        foreach (var assembly in assemblies)
        {
            var framework = TestFrameworks.DetectedIn(assembly.Reader);

            if (framework is null)
            {
                continue;
            }

            var hooks = new Hooks(types, framework.Lifecycle);
            var aroundEveryTest = hooks.AroundEveryTest(assembly);

            var classes = TestMethods.In(assembly, framework)
                .GroupBy(test => DeclaringTypeOf(assembly, test.Id));

            foreach (var tests in classes)
            {
                var around = hooks.AroundClass(assembly, tests.Key).Concat(aroundEveryTest).ToHashSet();

                foreach (var test in tests.Select(test => test.Id))
                {
                    edges.AddRange(around
                        .Where(hook => hook != test)
                        .Select(hook => new Edge(test, hook, EdgeProvenance.TestLifecycle)));
                }
            }
        }

        return edges;
    }

    private static TypeDefinitionHandle DeclaringTypeOf(GraphAssembly assembly, MethodId method) =>
        assembly.Reader
            .GetMethodDefinition((MethodDefinitionHandle)MetadataTokens.EntityHandle(method.Token))
            .GetDeclaringType();

    /// <summary>
    /// One framework's lifecycle, read against the types it applies to.
    /// </summary>
    private sealed class Hooks(FirstPartyTypes types, TestLifecycleHooks lifecycle)
    {
        /// <summary>
        /// What runs around every test in <paramref name="assembly"/>.
        /// </summary>
        internal List<MethodId> AroundEveryTest(GraphAssembly assembly)
        {
            var reader = assembly.Reader;
            var found = new List<MethodId>();

            foreach (var handle in reader.TypeDefinitions)
            {
                var type = reader.GetTypeDefinition(handle);

                found.AddRange(type.GetMethods()
                    .Where(method => TestMethods
                        .AttributeNames(reader, reader.GetMethodDefinition(method).GetCustomAttributes())
                        .Any(lifecycle.AssemblyMethodAttributes.Contains))
                    .Select(method => IdOf(assembly, method)));

                if (TestMethods.AttributeNames(reader, type.GetCustomAttributes()).Any(lifecycle.AssemblyTypeAttributes.Contains))
                {
                    found.AddRange(AroundClass(assembly, handle));
                }

                foreach (var fixture in FixturesOf(assembly, type, lifecycle.AssemblyFixtureInterfaces))
                {
                    found.AddRange(AroundClass(fixture.Assembly, fixture.Type));
                }
            }

            foreach (var fixture in AssemblyFixtures(assembly))
            {
                found.AddRange(AroundClass(fixture.Assembly, fixture.Type));
            }

            return found;
        }

        /// <summary>
        /// What runs around each test declared in <paramref name="type"/>: its own hooks, its base
        /// classes' — which still run for a derived class — and its class fixtures'.
        /// </summary>
        internal List<MethodId> AroundClass(GraphAssembly assembly, TypeDefinitionHandle type)
        {
            var found = new List<MethodId>();
            var seen = new HashSet<(int, TypeDefinitionHandle)>();
            var pending = new Queue<FirstPartyType>([new FirstPartyType(assembly, type)]);

            while (pending.TryDequeue(out var current))
            {
                if (!seen.Add((current.Assembly.Ordinal, current.Type)))
                {
                    continue;
                }

                var reader = current.Assembly.Reader;
                var definition = reader.GetTypeDefinition(current.Type);

                found.AddRange(definition.GetMethods()
                    .Where(method => IsHook(reader, reader.GetMethodDefinition(method)))
                    .Select(method => IdOf(current.Assembly, method)));

                foreach (var parent in types.Resolve(current.Assembly, definition.BaseType))
                {
                    pending.Enqueue(parent);
                }

                foreach (var fixture in FixturesOf(current.Assembly, definition, lifecycle.ClassFixtureInterfaces))
                {
                    pending.Enqueue(fixture);
                }
            }

            return found;
        }

        private bool IsHook(MetadataReader reader, MethodDefinition method)
        {
            var name = reader.GetString(method.Name);

            // An explicit implementation is named for its interface:
            // `System.IAsyncDisposable.DisposeAsync`.
            bool Named(string hook) =>
                name == hook || name.EndsWith("." + hook, StringComparison.Ordinal);

            return Array.Exists(ConstructionAndDisposal, Named)
                || lifecycle.MethodNames.Any(Named)
                || TestMethods.AttributeNames(reader, method.GetCustomAttributes()).Any(lifecycle.MethodAttributes.Contains);
        }

        /// <summary>
        /// The type argument of each of <paramref name="interfaces"/> that <paramref name="type"/>
        /// implements: <c>IClassFixture&lt;Database&gt;</c> gives <c>Database</c>.
        /// </summary>
        private IEnumerable<FirstPartyType> FixturesOf(
            GraphAssembly assembly,
            TypeDefinition type,
            IReadOnlyList<string> interfaces)
        {
            if (interfaces.Count == 0)
            {
                return [];
            }

            var reader = assembly.Reader;

            return type.GetInterfaceImplementations()
                .Select(handle => reader.GetInterfaceImplementation(handle).Interface)
                .Where(handle => handle.Kind == HandleKind.TypeSpecification)
                .Select(handle => reader
                    .GetTypeSpecification((TypeSpecificationHandle)handle)
                    .DecodeSignature(new SignatureNames(), genericContext: null))
                .Select(decoded => (Decoded: decoded, Generic: MetadataNames.WithoutInstantiation(decoded)))
                .Where(candidate => interfaces.Contains(candidate.Generic))
                .SelectMany(candidate => types.Named(
                    assembly,
                    MetadataNames.WithoutInstantiation(candidate.Decoded[(candidate.Generic.Length + 1)..^1])));
        }

        /// <summary>
        /// The <c>typeof</c> argument of each assembly attribute that names a fixture.
        /// </summary>
        private IEnumerable<FirstPartyType> AssemblyFixtures(GraphAssembly assembly)
        {
            var reader = assembly.Reader;

            if (lifecycle.AssemblyFixtureAttributes.Count == 0 || !reader.IsAssembly)
            {
                return [];
            }

            return reader.GetAssemblyDefinition().GetCustomAttributes()
                .Where(handle => TestMethods
                    .AttributeNames(reader, [handle])
                    .Any(lifecycle.AssemblyFixtureAttributes.Contains))
                .Select(handle => TypeArgumentOf(reader, reader.GetCustomAttribute(handle)))
                .OfType<string>()
                .SelectMany(name => types.Named(assembly, name));
        }

        private static MethodId IdOf(GraphAssembly assembly, MethodDefinitionHandle method) =>
            MethodId.Definition(assembly.Ordinal, MetadataTokens.GetToken(method));

        /// <summary>
        /// A single <c>System.Type</c> argument, serialised as an optionally assembly-qualified
        /// type name.
        /// </summary>
        private static string? TypeArgumentOf(MetadataReader reader, CustomAttribute attribute)
        {
            try
            {
                var blob = reader.GetBlobReader(attribute.Value);

                if (blob.Length < 2 || blob.ReadUInt16() != 1)
                {
                    return null;
                }

                return blob.ReadSerializedString()?.Split(',')[0].Trim();
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }
    }
}
