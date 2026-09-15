using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Reach.Assemblies;
using Reach.Changes;
using Reach.Graph;
using Reach.Join;
using Reach.Projects;
using Reach.Selection;

namespace Reach.Tests.Fixtures;

/// <summary>
/// A whole selection from two source strings: a library, a test project that references it,
/// and a list of declarations to treat as changed.
/// </summary>
/// <remarks>
/// Everything is in memory — the assemblies, their symbols, the project files and the analysis
/// scope — so a selection assertion costs milliseconds and no disk.
/// </remarks>
internal sealed class Selections : IDisposable
{
    private const string CorePath = "/repo/src/Core/Widget.cs";
    private const string TestsPath = "/repo/tests/Tests/WidgetTests.cs";

    private readonly List<IDisposable> open = [];

    private Selections()
    {
    }

    internal SelectionResult Result { get; private set; } = null!;

    internal CallGraph Graph { get; private set; } = null!;

    internal AnalysisScope Scope { get; private set; } = null!;

    internal IReadOnlyList<AssemblyInstance> Instances { get; private set; } = [];

    internal ChangedSet Changed { get; private set; } = ChangedSet.Empty;

    internal IReadOnlyList<ChangeEntry> Changes => Result.Changes;

    internal ProjectSelection TestProject => Result.Projects.Single();

    internal IReadOnlyList<string> SelectedNames =>
        [.. TestProject.Selected.Select(test => test.Test.Name).Order(StringComparer.Ordinal)];

    /// <param name="changed">
    /// Declarations to mark as changed, as "Type.member" — matched against the source's own
    /// declarations, so a typo fails loudly rather than selecting nothing.
    /// </param>
    /// <param name="added">Declarations to mark as added rather than modified.</param>
    internal static Selections Of(
        string coreSource,
        string testsSource,
        IEnumerable<string>? changed = null,
        IEnumerable<string>? added = null,
        bool includePaths = false)
    {
        var selections = new Selections();

        // The real xunit.v3.core is already in the compilation's reference set, because the
        // suite itself runs on it — so a fixture writing [Xunit.Fact] gets a genuine
        // xunit.v3.core 4.0.0 assembly reference, which is exactly what recognition reads. A
        // fixture that writes no Xunit type gets no reference, and falls back to whole-project
        // selection for the same reason a real project would.
        var core = Compiled.Assembly("Core", coreSource, CorePath);
        var tests = Compiled.Assembly("Tests", testsSource, TestsPath, references: [core]);

        var coreProject = Project("/repo/src/Core/Core.csproj", "Core");
        var testsProject = Project("/repo/tests/Tests/Tests.csproj", "Tests");

        var coreAssembly = selections.Open(0, "Core", core, CorePath);
        var testsAssembly = selections.Open(1, "Tests", tests, TestsPath);

        var scope = new AnalysisScope(
            [coreProject, testsProject],
            [testsProject],
            [
                new ExpectedAssemblyInstance(coreProject, "net10.0"),
                new ExpectedAssemblyInstance(testsProject, "net10.0"),
            ]);

        var instances = new[]
        {
            Instance(coreProject, coreAssembly),
            Instance(testsProject, testsAssembly),
        };

        var graphAssemblies = new[] { coreAssembly, testsAssembly };
        var graph = CallGraphBuilder.Build(graphAssemblies);

        var members = Members(coreSource, "src/Core/Widget.cs", changed, added)
            .Concat(Members(testsSource, "tests/Tests/WidgetTests.cs", changed, added))
            .ToArray();

        var joined = new SpanJoin(graphAssemblies, "/repo").ResolveAll(members);

        var changedSet = new ChangedSet(
            members,
            [],
            [],
            [new ChangedPath("src/Core/Widget.cs", ChangeStatus.Modified)],
            [],
            []);

        selections.Graph = graph.Graph;
        selections.Scope = scope;
        selections.Instances = instances;
        selections.Changed = changedSet;

        selections.Result = Selector.Select(
            graph.Graph,
            graphAssemblies,
            instances,
            scope,
            changedSet,
            joined,
            includePaths);

        return selections;
    }

    private static IEnumerable<ChangedMember> Members(
        string source,
        string path,
        IEnumerable<string>? changed,
        IEnumerable<string>? added)
    {
        var wanted = (changed ?? []).ToHashSet(StringComparer.Ordinal);
        var newOnes = (added ?? []).ToHashSet(StringComparer.Ordinal);

        foreach (var (typeName, type) in SourceRevision.DeclaredTypes(source))
        {
            foreach (var member in type.Members.Values)
            {
                var name = typeName.Split('.')[^1] + "." + member.Key.Name;

                if (!wanted.Contains(name) && !newOnes.Contains(name))
                {
                    continue;
                }

                yield return new ChangedMember(
                    typeName,
                    member.Key,
                    newOnes.Contains(name) ? MemberChange.Added : MemberChange.Modified,
                    path,
                    member.IsCompileTimeConstant,
                    member.Span);
            }
        }
    }

    private static ProjectFile Project(string path, string name) =>
        new(Paths.Normalise(path), name, ["net10.0"], [], []);

    private static AssemblyInstance Instance(ProjectFile project, GraphAssembly assembly) =>
        new(
            new ExpectedAssemblyInstance(project, "net10.0"),
            new ScannedAssembly(
                Paths.Normalise("/repo/bin/" + assembly.Name + ".dll"),
                assembly.Name,
                Guid.NewGuid(),
                assembly.Framework,
                IsFirstParty: true,
                []));

    private GraphAssembly Open(int ordinal, string name, CompiledAssembly compiled, string documentPath)
    {
        var reader = new PEReader(ImmutableArray.Create(compiled.Image));
        var symbols = MetadataReaderProvider.FromPortablePdbImage(ImmutableArray.Create(compiled.Symbols));

        open.Add(reader);
        open.Add(symbols);

        return new GraphAssembly(
            ordinal,
            name,
            TargetFrameworkMoniker.Parse("net10.0"),
            reader.GetMetadataReader(),
            reader,
            symbols.GetMetadataReader());
    }

    public void Dispose()
    {
        foreach (var item in open)
        {
            item.Dispose();
        }
    }
}
