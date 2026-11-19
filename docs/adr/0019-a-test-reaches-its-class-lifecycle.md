# A test reaches its class's lifecycle through synthesised edges

A test framework runs more of a test class than the test method. It runs the constructor
(and so every field initialiser), the static constructor, `Dispose`/`DisposeAsync`, and the
setup and teardown the class declares, and it creates the fixtures the class is given. It
does all of this by reflection, so no instruction connects a test method to any of it. Until
this decision, a change reached only from there selected nothing. This happened silently,
on ordinary test code. Reach's own dogfood run found it: `AcceptanceTests` was not selected
when `RepositoryRoot`, which its constructor reaches, changed.

So the graph carries a fourth synthesised edge kind, **test lifecycle**: from each test
method to every lifecycle member its framework runs around it. It is synthesised rather
than widened for the same reason containment is
([ADR-0004](0004-call-graph-edges-carry-provenance.md)): the framework's contract makes the
control flow certain, so a selection through it reads `compiled` and no future narrowing
may remove it.

## What counts as lifecycle

For every framework: the test class's instance constructors and static constructor, and any
method named `Dispose` or `DisposeAsync`, explicit implementations included. Base classes
are followed, because a hook a base class declares still runs for a derived test class.

Per framework, as data on its row of the recognition table, next to its test attributes:

- **xUnit**: `InitializeAsync` (`IAsyncLifetime`); the type argument of `IClassFixture<T>`
  for that class; the type argument of `ICollectionFixture<T>` and of
  `[assembly: AssemblyFixture(typeof(T))]` for every test in the assembly.
- **NUnit**: `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, `[OneTimeTearDown]` for that class;
  a `[SetUpFixture]` class's lifecycle for every test in the assembly.
- **MSTest**: `[TestInitialize]`, `[TestCleanup]`, `[ClassInitialize]`, `[ClassCleanup]` for
  that class; `[AssemblyInitialize]`, `[AssemblyCleanup]`, `[GlobalTestInitialize]`,
  `[GlobalTestCleanup]` for every test in the assembly.

Two of these are wider than the framework's own scope. A collection fixture runs only for
its collection, and a `[SetUpFixture]` only for its namespace and below. Matching collection
names and namespaces would be exact, but taking the whole assembly is simpler and errs
towards over-selection. Both are in the limitations register.

## Where the edges are built

Test recognition belongs to selection, not to the graph, so selection derives the edges from
the same assemblies and hands them to the graph builder before the walk. The graph treats
them like any other edge. Hop-by-hop paths show the hop from test to constructor, and the
reverse walk needs no change.

## Considered and rejected

**Treat a test's lifecycle members as extra entry points at selection time**, with no edge.
This needs less plumbing. But a selection through a constructor would have no edge to
explain it. Its path class would have to be patched in, and it would give provenance a
second, implicit channel that ADR-0004 exists to prevent.

**Wait for framework models (PRD §6).** Those are about production frameworks a host calls
into, and ADR-0008 accepted that hole for M1. This one is narrower and different: Reach
already recognises the test framework, and the hole sits under every test class that has a
constructor. Leaving it would be an unnamed hole, which ADR-0008 does not allow.
