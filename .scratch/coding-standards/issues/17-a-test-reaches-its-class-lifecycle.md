# 17: A test reaches its class's constructor, disposal and setup

**What to build:** A change reached only through a test class's constructor, disposal or framework setup/teardown selects that class's tests. Today the reverse walk has no edge from a test method to the code the framework runs around it, so such a change selects nothing — silent under-selection. Found by ticket 13's dogfood run (see its `## Comments`): with base `HEAD~3`, `AcceptanceTests` was not selected although its constructor calls `FixtureSolution.Build` → `RepositoryRoot`, which changed. Maintainer decision: "Fix the bugs".

- Model the framework's implicit calls as **synthesised** edges from each test method to the lifecycle members of its class, with their own provenance, so path class stays `compiled` and `--paths` shows the hop (ADR-0004).
- Cover the frameworks Reach recognises: xUnit v2/v3, NUnit, MSTest. Constructors (which carry field initialisers), the static constructor, `Dispose`/`DisposeAsync`, xUnit's `IAsyncLifetime` and class/collection/assembly fixtures, NUnit's `[SetUp]`/`[TearDown]`/`[OneTimeSetUp]`/`[OneTimeTearDown]` and `[SetUpFixture]`, MSTest's `[TestInitialize]`/`[TestCleanup]`/`[ClassInitialize]`/`[ClassCleanup]`/`[AssemblyInitialize]`/`[AssemblyCleanup]`.
- Lifecycle knowledge is data on the recognition table, like the test attributes are.
- Record the decision in an ADR (0019) and update `docs/limitations.md` for any imprecision it introduces.

**Blocked by:** None

**Status:** ready-for-agent

- [x] A change reached only from a test class's constructor or a field initialiser selects every test in that class, and no test in another class
- [x] The same for `Dispose`/`DisposeAsync`, the static constructor, and xUnit `IAsyncLifetime.InitializeAsync`
- [x] The same for a fixture: xUnit `IClassFixture<T>`, `ICollectionFixture<T>` and `[assembly: AssemblyFixture]`
- [x] The same for NUnit and MSTest setup/teardown attributes, including hooks inherited from a base class
- [x] The lifecycle edge carries synthesised provenance: the selection reads `compiled` and `--paths` includes the hop
- [x] Full suite passes; build and format gates remain clean

## Comments

- Confirmed the hypothesis by reading the code: test methods are recognised only in selection (`TestMethods`), and the graph had no edge from a test to anything the framework runs by reflection. Neither `docs/limitations.md` nor any ADR covered it; ADR-0008's accepted holes are about production code a host calls, not test classes.
- New synthesised provenance `EdgeProvenance.TestLifecycle`. `Selection/TestLifecycle.EdgesIn` derives the edges from the recognition table and passes them to `CallGraphBuilder.Build`, because test recognition belongs to selection, not the graph. The lifecycle hooks are data on each `TestFramework` row (`TestLifecycleHooks`). Decision recorded in ADR-0019.
- Hooks covered. All frameworks: instance constructors (field initialisers included), the static constructor, `Dispose`/`DisposeAsync` (explicit implementations included), followed up the base-class chain across first-party assemblies. xUnit: `IAsyncLifetime.InitializeAsync`, `IClassFixture<T>` (that class), `ICollectionFixture<T>` and `[assembly: AssemblyFixture]` (whole assembly). NUnit: `[SetUp]`, `[TearDown]`, `[OneTimeSetUp]`, `[OneTimeTearDown]`, `[SetUpFixture]` (whole assembly). MSTest: `[TestInitialize]`, `[TestCleanup]`, `[ClassInitialize]`, `[ClassCleanup]`, `[AssemblyInitialize]`, `[AssemblyCleanup]`, `[GlobalTestInitialize]`, `[GlobalTestCleanup]`.
- `docs/limitations.md` gains two entries: an over-selection (collection fixtures and setup fixtures reach the whole assembly) and an under-selection residue (hooks not modelled, e.g. xUnit `BeforeAfterTestAttribute`, NUnit `ITestAction`). `CONTEXT.md` gains **Test lifecycle**.
- Tests: `tests/Reach.Tests/Selection/TestLifecycleTests.cs` (24 cases, over real IL; NUnit and MSTest through stand-in framework assemblies with the real names and versions). No Contoso change was needed. Full suite: 553 passed.
- Not fixed, separate finding: a test method declared on an abstract base class and inherited by a derived test class is recognised under the base's name, so its filter probably does not match what the runner reports (`Derived.Test`; not verified), and the derived class's own lifecycle is not reached. Not in `docs/limitations.md` either.
