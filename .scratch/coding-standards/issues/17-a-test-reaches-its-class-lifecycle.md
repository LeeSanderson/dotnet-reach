# 17: A test reaches its class's constructor, disposal and setup

**What to build:** A change reached only through a test class's constructor, disposal or framework setup/teardown selects that class's tests. Today the reverse walk has no edge from a test method to the code the framework runs around it, so such a change selects nothing — silent under-selection. Found by ticket 13's dogfood run (see its `## Comments`): with base `HEAD~3`, `AcceptanceTests` was not selected although its constructor calls `FixtureSolution.Build` → `RepositoryRoot`, which changed. Maintainer decision: "Fix the bugs".

- Model the framework's implicit calls as **synthesised** edges from each test method to the lifecycle members of its class, with their own provenance, so path class stays `compiled` and `--paths` shows the hop (ADR-0004).
- Cover the frameworks Reach recognises: xUnit v2/v3, NUnit, MSTest. Constructors (which carry field initialisers), the static constructor, `Dispose`/`DisposeAsync`, xUnit's `IAsyncLifetime` and class/collection/assembly fixtures, NUnit's `[SetUp]`/`[TearDown]`/`[OneTimeSetUp]`/`[OneTimeTearDown]` and `[SetUpFixture]`, MSTest's `[TestInitialize]`/`[TestCleanup]`/`[ClassInitialize]`/`[ClassCleanup]`/`[AssemblyInitialize]`/`[AssemblyCleanup]`.
- Lifecycle knowledge is data on the recognition table, like the test attributes are.
- Record the decision in an ADR (0019) and update `docs/limitations.md` for any imprecision it introduces.

**Blocked by:** None

**Status:** ready-for-agent

- [ ] A change reached only from a test class's constructor or a field initialiser selects every test in that class, and no test in another class
- [ ] The same for `Dispose`/`DisposeAsync`, the static constructor, and xUnit `IAsyncLifetime.InitializeAsync`
- [ ] The same for a fixture: xUnit `IClassFixture<T>`, `ICollectionFixture<T>` and `[assembly: AssemblyFixture]`
- [ ] The same for NUnit and MSTest setup/teardown attributes, including hooks inherited from a base class
- [ ] The lifecycle edge carries synthesised provenance: the selection reads `compiled` and `--paths` includes the hop
- [ ] Full suite passes; build and format gates remain clean

## Comments
