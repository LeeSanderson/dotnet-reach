# Reach — Product Requirements

**Status:** Approved v0.2 · 9 September 2026
**Owner:** Lee Sanderson

Amended from v0.1 during walking-skeleton design. What changed, and which decision record
drove it, is listed in the closing appendix.

---

## The bottom line, in plain terms

Large .NET codebases run their entire test suite on every pull request, even though most changes only affect a small part of the system. That wastes CI minutes and slows down feedback.

Reach is a command-line tool that looks at what a developer changed, works out which tests could possibly be affected by that change, and tells the build to run only those. It works out the answer by reading the compiled application — following the chain of "who calls what" backwards from the changed code until it arrives at a test — rather than by watching previous test runs.

The name is the question it answers: *what does this change reach?*

---

## How to read this document

Section 1 states the problem and who has it. Section 2 fixes the scope, which matters because "run fewer tests" is a wide idea and this document is only about one narrow way of doing it. Section 3 defines every term before it is used elsewhere. Sections 4 to 8 are the requirements. Section 9 records the decisions already taken and, more importantly, *why* — those reasons are the defence when someone proposes the obvious alternative. Sections 10 to 12 cover what is deliberately excluded, what is still unknown, and how we would know if this worked.

Where a design decision has superseded something this document said in v0.1, the passage cites the decision record that carries the reasoning. Those records live in [docs/adr/](docs/adr/) and win where they and this document disagree.

---

## 1. Problem and audience

### 1.1 The problem

A mature .NET service or platform typically has a test suite that grows faster than the team's tolerance for waiting. A 20 to 40 minute suite on every pull request is common. The great majority of that work is redundant on any given change: a fix to an invoice formatter does not need the authentication tests to run.

Teams currently respond in ways that all cost something:

- They accept the wait, and the cost is cycle time.
- They split the suite by hand into "fast" and "slow" tiers, and the cost is that the split rots and the slow tier gets skipped.
- They buy a commercial test-selection product, and the cost is licence spend plus a standing infrastructure commitment.
- They do nothing and gradually stop trusting the suite.

### 1.2 Who this is for

**Primary:** A .NET platform or infrastructure engineer at an organisation with a large solution — dozens to hundreds of projects — who owns the CI pipeline and is measured on build times. They can change the pipeline but cannot get the whole engineering org to restructure the codebase.

**Secondary:** A consultant assessing a client's delivery pipeline who needs a tool that can be introduced in an afternoon, on a repository they have never seen, without asking the client to install a service or sign a contract.

**Explicitly not the audience yet:** an individual developer wanting a continuous local test runner. That need is real and is addressed by NCrunch and by continuous testing in Rider and Visual Studio. Reach may serve it later (§7) but it is not the reason the tool exists.

---

## 2. Scope

**The unit of analysis is a single .NET solution built from one repository, at one commit.**

Reach answers exactly one question: *given a set of changed source files, which test methods in this solution could be affected?* It emits that answer as the commands the pipeline should run, plus a report explaining them (§4.3). It does not run the tests, own the pipeline, or reason across repository boundaries.

Things that are adjacent and deliberately outside this scope:

- Selecting which *services* to deploy after a change. Different question, different graph.
- Cross-repository impact analysis. A separate and much harder problem.
- Test quality, flakiness detection, or ordering.
- Anything in a language other than C#, F# or VB compiled to .NET IL.

---

## 3. Terms

Defined here so that later sections do not need to explain themselves.

**Test impact analysis (TIA).** The general practice of running only the tests affected by a change instead of the whole suite.

**Selection.** The set of tests Reach decides to run. **Over-selection** means running more tests than strictly necessary — wasteful but safe. **Under-selection** means failing to run a test that would have caught a regression — the failure mode that destroys trust in the tool and must be treated as a correctness bug, not a tuning issue. M1 takes a bounded and documented exception to that rule; it is stated in §8 rather than here, because the rule is the standing one and the exception is temporary.

**Static analysis.** Determining impact by examining code without executing it. The approach Reach takes.

**Coverage-based analysis.** Determining impact by recording, during a previous test run, which code each individual test executed, and consulting that record. The approach taken by Datadog Test Impact Analysis, Sealights and Teamscale. Rejected here; see §9.1.

**Call graph.** A directed graph whose nodes are methods and whose edges are "method A can invoke method B". Reach builds this from compiled output and walks it backwards.

**Reverse reachability.** Starting from the changed methods and following call edges *backwards* to find every method that could lead to them, stopping at test methods. This is the core algorithm.

**IL (Intermediate Language).** The instruction set that .NET compiles to. Reading IL from compiled assemblies is much faster than parsing source, and reveals call targets that source does not obviously show.

**MVID (Module Version Identifier).** A GUID embedded in every compiled assembly. With deterministic builds — the default in modern .NET — it is a function of the compilation inputs, so an unchanged MVID means an unchanged assembly.

**Framework model.** A small description telling Reach about a call edge that exists at runtime but not in the compiled instructions — for example, that `IMediator.Send(new FooCommand())` reaches `FooCommandHandler`. See §6.

**Blind spot.** A runtime call edge that neither IL analysis nor any loaded framework model can see. Reflection, plugin loading from disk and some convention-based dependency injection produce blind spots. Blind spots cause under-selection, so the tool's response to a suspected blind spot is to widen the selection, never to narrow it.

**Project graph.** The dependency graph between `.csproj` files, derived from `ProjectReference` elements. Much coarser than the call graph and available without compiling anything.

---

## 4. What Reach does

### 4.1 The pipeline

1. **Determine the changed set.** Diff the working tree against a baseline (a git ref in CI; the last passing run locally). Parse only the changed C# files with Roslyn, hash method bodies, and produce a list of changed, added and removed method symbols.
2. **Ensure compiled output exists.** Either run the build, or accept the caller's assertion that it has already run. See §5.
3. **Build the call graph.** Read every assembly in the analysis scope (§8.2) using `System.Reflection.Metadata`. Collect `call`, `callvirt`, `newobj` and `ldftn` targets. Build a type hierarchy index in the same pass so virtual and interface dispatch can be widened. Invert to get reverse edges.
4. **Apply framework models.** Each loaded model may add edges the IL does not contain.
5. **Walk backwards.** From the changed method set, traverse reverse edges to every reachable test method.
6. **Emit.** Produce the argument vectors the pipeline should invoke, plus a machine-readable report of what was selected, what was not, and why (§4.3).

### 4.2 Selection rules

A test is selected if any of the following hold:

- It is reverse-reachable from a changed method.
- Its own source changed.
- It is new since the baseline. Reach reads only the current compiled output and cannot enumerate the baseline's tests without building it, so newness is *derived from the change set* rather than by comparing two test lists. That derivation depends on change detection seeing attribute-level edits, which is not free; see §11.
- It lives in a project that Reach has declared unanalysable (see §8).

There were five rules in v0.1. **"It failed in the previous run" has been removed:** it requires state carried between runs, and Reach persists none — [ADR-0001](docs/adr/0001-reach-persists-no-state-between-runs.md).

The rules are not interchangeable, and only reverse-reachability is an analysis result. Each selected test therefore records *which* rules selected it, so a reader of the report can tell a graph conclusion from a fallback.

### 4.3 Output contract

Reach emits **one JSON report per run, and complete argument vectors — not a filter string — for each (test project, target framework) pair.**

v0.1 said "a filter suitable for `dotnet test --filter`". That is too small in both halves. The correct rendering depends on the test framework's generation, its package version and which runner host is executing it; and a selection past roughly 100 test methods does not fit on a command line at all, so long selections travel through each host's own private file channel or are split across several invocations — [ADR-0009](docs/adr/0009-per-host-private-filter-channels-over-runsettings.md). Reach owns the whole command line rather than contributing a fragment to someone else's, and each project's entry holds an `invocations` **array**, which has the useful consequence that an empty selection is an empty array rather than a filter string that would run everything.

The report is the audit trail: without it nobody can debug a surprising result, and a tool nobody can debug gets switched off. It carries more than an enumeration in this section could keep accurate — per-project selection modes and delivery mechanisms, the rules that selected each test, the weakest edge on the path that reached it, every change that reached nothing, notices with stable codes, and a run envelope. **The schema, not this section, is the contract.** It is versioned, documented, and a product surface with a compatibility promise: consumers are expected to build on it, so it may grow additively and may not break.

---

## 5. Build handling

Reach analyses compiled assemblies, so compiled assemblies must exist and must correspond to the current source. **Correspondence is verified, not assumed, and verified in both modes** — [ADR-0003](docs/adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md). Reach compares the source files it diffed against the per-document checksums the compiler recorded in the portable debug symbols. A mismatch is an error, not a warning: analysing binaries from a different commit is precisely the silent under-selection §8's invariant exists to prevent.

**Two CI modes, both explicit:**

- **Default.** Reach invokes `dotnet build` itself. On a tree that is already built this is a fast no-op, because MSBuild's own up-to-date checking decides whether work is needed — Reach does not implement its own up-to-date check. On a clean checkout it is a full build.
- **`--no-build`.** Reach uses the output already on disk, for pipelines where a previous step has just built the solution. v0.1 documented this as an unverifiable assertion by the caller. It is no longer: the checksum comparison applies here too, so a stale or foreign output is caught rather than trusted.

**If the build fails, Reach exits with an error and selects nothing.** There is no degraded mode. A failed build already fails the pipeline, so a clever test selection over a broken tree has no consumer.

**What the correspondence check defends against:** file timestamps are not a trustworthy freshness signal on a warm CI agent or a cache-restored workspace, because git does not preserve modification times. Checking out an older commit onto a warm tree can leave source files older than the binaries beside them, and MSBuild will then correctly conclude nothing needs rebuilding while the binaries belong to a different commit. v0.1 concluded from this that freshness could not be established and left `--no-build` on a warm agent as the caller's risk. Source checksums are not a timestamp heuristic — they are the compiler's own record of the bytes it compiled — so this case is now detected in both modes. The consequence is that **debug symbols must be present in the build output**; `DebugType` defaults to `portable` in Release as well as Debug, so this does not force a Debug build.

---

## 6. Framework models

### 6.1 Why they exist

IL shows compiled call instructions. It does not show edges established at runtime by convention. The clearest example: `_mediator.Send(new CreateOrderCommand())` has no compiled edge whatsoever to `CreateOrderCommandHandler`. Without help, Reach would skip every handler test and report success.

Framework models supply those edges. The precedent is CodeQL's library models, which solve the same problem for dataflow analysis.

### 6.2 Rules

- **Models are purely additive.** A model may add edges. It may never remove or suppress one. This makes load order irrelevant, makes conflicts impossible, and guarantees no model can cause an under-selection. It also means a model that crashes can be caught, skipped and reported without compromising correctness.
- **Models declare what they cover** — package identity and version range. This is what powers the fail-loud check in §8, so it is required, not documentation.
- **Four sources, one loader:** models embedded in Reach as data; models embedded as code; customer-supplied data models; customer-supplied code models.
- **Code models bind to a Reach contracts assembly with zero package dependencies** — never to `Microsoft.CodeAnalysis` or a metadata-reader type. This avoids the version diamond that makes Roslyn analyzers painful. Code models load into an isolated `AssemblyLoadContext` with the contracts assembly as the only shared type identity.
- **The contracts assembly is not published until the built-in models have shaped it.** Write eight to ten models against a private API first. Once published, it evolves additively and never breaks, so it must stay deliberately small.

### 6.3 Launch set

MediatR, MassTransit, ASP.NET Core routing (controller actions and minimal API endpoints), `IHostedService`, FluentValidation, AutoMapper profiles.

---

## 7. Local mode

Secondary to CI and to be built only after CI mode is proven.

The local loop watches the filesystem, rebuilds incrementally, reselects and reruns. Three things differ from CI:

- **The baseline is the last passing run, not a git ref.** Diffing against `HEAD` means that an hour into a session everything is "changed" and the selection creeps toward the full suite.
- **The call graph is cached in memory, keyed by MVID per assembly.** The no-cache rule in §9.3 is a CI rule. A long-lived watch process already holds the graph in memory; re-reading three hundred assemblies when one changed is pure waste. Because the cache dies with the process, it introduces no cross-run staleness.
- **The latency budget is roughly two seconds** from edit to tests running.

**First step is a spike, not a build:** wire `dotnet watch` to invoke the Reach CLI and measure the real round trip. Only build a persistent daemon if the measured number misses the budget.

---

## 8. Correctness model

This section is the one that decides whether anyone leaves the tool switched on.

**The invariant:** *an empty or reduced selection must never be reachable from missing data.* Every failure widens the selection or stops the run. Nothing narrows it.

Concretely:

| Situation | Response |
|---|---|
| Build fails | Error, exit non-zero, select nothing, run nothing |
| A framework model throws | Skip that model, report it, treat its area as unmodelled |
| A referenced package has no model | Drop the projects using it to whole-project selection and say so in the report |
| The project graph cannot be parsed | Run everything |
| Assembly missing from output | Error — this indicates a build problem, not an analysis problem |
| Binaries do not correspond to the source diffed | Error — see §5 |

### 8.1 M1's bounded exception

**M1 accepts named under-selection rather than widening for every hole** — [ADR-0008](docs/adr/0008-m1-accepts-named-under-selection.md). This is a conscious owner override of the invariant above, signed off here and not only in the decision record, because it is the largest deviation from v0.1 and the one most likely to be mistaken for a bug and "fixed".

The reasoning is that the walking skeleton is a proof of concept, and working code with stated limits is worth more than an exhaustively-specified tool that does not exist. Coverage then grows by closing named holes one at a time against real measurements. The largest hole accepted is **first-party members invoked only from outside the analysis scope** — a framework base-class override the host calls and first-party code never does.

**The exception is conditional, and the condition is load-bearing.** It holds only while every accepted hole is recorded in the **limitations register** and surfaced in the report where a user deciding whether to trust a selection will see it. §10 already scopes the product claim to conservatism *plus disclosure*: a named, reported hole keeps that promise, and a silent one breaks it. If the register lapses or a hole stops being reported, the exception lapses with it and the invariant governs again. The register is therefore a deliverable of M1, not a follow-up.

The invariant itself is unchanged, and it is what M2 returns to. Beyond M1 there is no standing licence to under-select.

### 8.2 Analysis scope

**The analysis scope is the union of the transitive project closures of the solution's test projects** — [ADR-0002](docs/adr/0002-analysis-scope-is-the-union-of-test-project-closures.md). v0.1 said "always every assembly in the solution", which is a near-miss: code outside every test closure cannot execute in any test process, so a change to it cannot affect a test, and defining scope this way is what makes it legitimate to point Reach at a single test project. Closures are derived from project references in the project files, not from assembly references in metadata, because the compiler omits references to assemblies whose types are never named.

**Build scope may be narrowed; changed-side analysis scope may not.** Analysing only the projects that transitively depend on the project that changed is the forbidden narrowing. Reverse walks cross interface boundaries into projects that do not depend on the changed one — an implementation's callers usually reference only the interface, so a caller's project is not a dependent of the implementation's project and would be excluded despite being affected. These are two different sets and conflating them is the most attractive available mistake.

Test-side scoping is a different operation: it follows runtime executability rather than dependency direction, which is what makes it sound. A pipeline that points Reach at a single test project must still restore the whole solution's output, or scope cannot be established and Reach errors rather than answering narrowly.

### 8.3 Shadow mode

**Shadow mode is a launch requirement, not a nice-to-have.** Reach must be able to run the selection, then run the whole suite anyway, and record every test it would have skipped that failed. No team should rely on the selection until that number has been zero for a meaningful period on their own codebase. Building and interpreting this harness is comparable work to the selector itself.

---

## 9. Decisions taken, and why

### 9.1 Static analysis, not coverage-based

Coverage-based selection is more precise: it records which implementation actually ran, so it never has to widen through an interface.

It was rejected because it requires standing infrastructure. The index cannot live in the repository — it would be a hot file generating constant merge conflicts — so in practice it lives in a service, keyed by commit. That means a main-branch build running the full suite under per-test instrumentation on every commit, storage serving indexes by commit, and a policy for cache misses, which are frequent because a long-lived branch's merge-base can be far behind.

Reach must be installable on an unfamiliar repository in an afternoon with no infrastructure ask. That single requirement rules coverage out.

The cost accepted: over-selection through interface dispatch, and blind spots requiring framework models.

**The .NET team is building the rejected approach, which is the best available evidence for this section.** Work on `dotnet test --affected-tests` is visible in unreleased branches, gated behind an environment variable, with the selection engine in a private extension that is not yet published. Its design needs exactly the infrastructure this section rejected: coverage instrumentation over the suite, a persisted map of test to code, a cached store keyed by commit, an accepted cache-miss rate, and a full-suite fallback leg for when the cache misses. That is a worked example of the standing commitment, authored by the people best placed to make it cheap, and it is worth more than the argument alone. The two approaches are complementary rather than competing, and M1 proceeds unchanged — but see §11 for what it costs this document to be honest about it.

### 9.2 Compiled IL, not Roslyn source analysis

`MSBuildWorkspace.OpenSolutionAsync` takes minutes on a large solution, which would consume the saving. Reading assembly metadata takes seconds. IL also includes source-generated code without re-running generators, and exposes lambdas as concrete methods.

v0.1 put async state machines and generic instantiations in that same list. Both claims need qualifying, and the gap between them and the truth is where two of M1's accepted holes come from — [ADR-0006](docs/adr/0006-a-graph-node-is-an-il-method-definition.md).

- **An async body is a concrete method, but nothing in first-party IL calls it.** Control reaches `MoveNext` through `AsyncTaskMethodBuilder.Start` inside the BCL, so a reverse walk over first-party call instructions alone never arrives at the body. A **containment edge** from the kernel method to its state machine exists to reconnect it. Visible in IL, yes; reachable for free, no.
- **A generic instantiation is exposed at the call site, not as a distinct definition.** There is one definition and many `MethodSpec` references to it, which is why method identity collapses instantiations rather than tracking them — and why a call whose target is only known through a type argument cannot be resolved from the definition table.

Roslyn is still used, but only to diff the changed files — never to load a solution.

### 9.3 No persisted call graph in CI

Rebuilding from the binaries every run makes graph staleness impossible by construction, which removes an entire class of invalidation bug for a cost of a few seconds against a suite measured in tens of minutes. Local mode is the deliberate exception (§7).

### 9.4 Performance constraints this implies

Method identity must be integers derived from metadata tokens, never strings — the difference between seconds and minutes at solution scale, and painful to retrofit. The type hierarchy index must be built in a single pass; resolving implementations per call site is accidentally quadratic and will look fine on a sample repository and fail on a client's.

---

## 10. Non-goals

- Running tests. Reach emits the commands; the pipeline runs them.
- Replacing NCrunch. Reach selects after a build; NCrunch selects on an uncompiled edit.
- A hosted service, dashboard or account.
- Guaranteeing perfect selection. No test impact analysis is exact, and any documentation claiming otherwise is lying. Reach's claim is that it is conservative and that it says so when it cannot see.

---

## 11. Open questions and risks

**The build-to-test ratio is the viability test, and measuring it has been consciously deferred, not forgotten.** In a clean CI checkout the full build is unavoidable, so Reach only ever saves test time. A codebase that builds in six minutes and tests in twenty-five is a strong candidate. One that builds in twenty and tests in eight cannot be helped by any selection tool. v0.1 called for measuring this on two or three real client solutions before further engineering. **The owner has waived that gate for M1:** gating a solo project on access to client solutions would stall it, and the ratio only decides whether the skeleton is worth building, which is already decided. It returns as an adoption question — a team with an unfavourable ratio should be told so rather than sold a selector — and it remains the first thing to measure before anyone invests in M2.

**Over-selection in layered DI codebases is the main technical risk.** Widening through interfaces in a codebase where everything sits behind one may select 80 to 90% of the suite, making the analysis pure overhead. Unknown until measured on a real solution, and it may vary enormously between codebases. A possible mitigation — narrowing widening using container registrations — is unexplored, conflicts with §6.2's additive-only rule and §8's invariant, and would need shadow mode to prove it safe.

**Coverage-based selection is more precise exactly where Reach is weakest.** This is the honest form of the risk above. A coverage map records the implementation that actually ran, so it does not widen through interface dispatch at all — the failure mode that a layered DI codebase makes worst is the one coverage does not have. Reach's advantage is not precision; it is that it needs nothing standing.

**"No infrastructure ask" is a narrower moat than §9.1 assumes.** Once Microsoft's extension is published, adopting coverage-based selection on Azure DevOps could be a package reference, a `global.json` block and a cache task — which is not an afternoon of consulting, but nor is it a service to procure. The moat holds best for the secondary consultant audience, for non-Azure CI, and for the large majority of suites not yet on Microsoft.Testing.Platform. **Revisit trigger: the extension published publicly with a local filesystem provider.** At that point the "no infrastructure" argument should be re-argued from scratch rather than cited.

**Rule 3 depends on attribute-level change detection.** §4.2 selects a test that is new since the baseline, but newness is derived from the change set, and the known failing case is adding `[Fact]` to a method that already existed: the body hash is unchanged, so a body-level diff sees nothing and the new test is not selected. Either change detection notices attribute edits on member declarations, or this is a named hole under §8.1. Open, and owed a decision before the change-detection design is fixed.

**Unmodelled-framework detection is harder than modelling.** Writing a MediatR model is an afternoon. Reliably detecting that a solution contains indirection nobody has modelled is fuzzy, and it is the feature that makes the tool safe.

**Data-driven test identity — settled.** Theories and parameterised tests generate names differently per framework and sometimes non-deterministically. v0.1's proposed resolution is adopted: index and select at test-method granularity, never per test case. This accepts slight over-selection to remove a class of bug, and it is confirmed safe — a method-level filter does match every case of a parameterised test in all three supported frameworks.

**Is standalone mode supported at all? — settled.** Both modes ship (§5). The question rested on freshness being uninferable, and it no longer is: correspondence is verified from debug-symbol checksums in both modes, so `--no-build` does not trade safety for speed.

---

## 12. Milestones and success criteria

**M0 — Viability check (days). Waived for M1**, for the reasons in §11. Not deleted: it is the first thing to measure before M2 is funded with anything but the owner's own time.

**M1 — Walking skeleton.** Larger than v0.1's one-line description, because design has made the skeleton's real extent visible. It carries:

- Roslyn changed-member diff against a baseline, keyed on the declared type rather than the file path ([ADR-0005](docs/adr/0005-change-detection-is-keyed-on-declared-type.md)), over a change set spanning commits since the baseline plus staged, unstaged and untracked work.
- Source-binary correspondence verification in both build modes, and assembly discovery from the output directories so that `--no-build` works at all.
- An IL call graph whose nodes are method definitions per assembly and per target framework, with **every** target framework of a multi-targeted project analysed, and edges carrying provenance across seven edge kinds in three safety classes ([ADR-0004](docs/adr/0004-call-graph-edges-carry-provenance.md), [ADR-0006](docs/adr/0006-a-graph-node-is-an-il-method-definition.md)).
- Widening through virtual and interface dispatch, bounded by the inferred receiver type ([ADR-0007](docs/adr/0007-widening-targets-the-inferred-receiver-type.md)), over a type hierarchy index built in a single pass.
- The reverse walk, and the fallbacks that catch what it cannot map: whole-assembly, whole-type and whole-project widening. **Whole-project fallback is M1, not M2** — it is what an unrecognised test framework or an unparseable input degrades to, so the skeleton cannot ship without it.
- Test recognition and filter rendering for xUnit (v2 and v3), NUnit and MSTest, across multiple runner hosts and hence multiple filter dialects, with the framework-agnostic JSON report as the canonical form every dialect is rendered from.
- The report and delivery contract of §4.3, including notices and exit codes.
- The limitations register, on which §8.1's exception depends.

Still **no framework models**, and correctness demonstrated on committed fixture solutions rather than a client codebase.

**M2 — Trustworthy (about a quarter).** Framework models plus loader; unmodelled-framework detection; shadow mode with miss-rate reporting; and closing the holes M1 named under §8.1, at which point the §8 invariant governs without exception. Run in shadow on a real codebase for a month.

**M3 — Local mode.** Only after M2, and only if the `dotnet watch` spike shows the latency is worth pursuing.

**Success criteria:**

- **Correctness (M2):** zero missed failures in shadow mode over a month of real use on a real codebase. This is a gate, not a target.
- **Correctness (M1):** every under-selection hole Reach knows about is in the limitations register and reported when it applies, asserted by a test rather than by review. M1 cannot meet the M2 gate — shadow mode is what measures it — so this is what §8.1's exception is held to instead.
- **Saving:** median PR selects under 40% of the suite on a representative solution. Below that, over-selection has eaten the value.
- **Adoption cost:** a competent engineer can add Reach to an unfamiliar pipeline in under an hour, using only documentation.
- **Debuggability:** any surprising selection can be explained from the JSON report without rerunning the tool.

---

## Appendix — naming

**Reach** was chosen because reverse reachability is literally the algorithm, and "what does this change reach?" is the question a developer is actually asking. It is short, works as both verb and noun, and reads naturally as a command: `dotnet reach select`, `dotnet reach watch`.

Package name would be `dotnet-reach`, invoked as `dotnet reach`.

---

## Appendix — amendments since v0.1

v0.1 was approved before any design work. Designing the walking skeleton overturned parts of
it. Each amendment below is recorded so that a reader holding v0.1 can see what moved and why;
the reasoning lives in the decision record cited, not here.

| § | What changed | Driven by |
|---|---|---|
| §3, §8 | Under-selection is still a correctness bug, but M1 takes a bounded exception: named holes in a limitations register, surfaced in the report, instead of widening for every hole. New §8.1. **The largest deviation from v0.1**, and an owner override rather than a design consequence | [ADR-0008](docs/adr/0008-m1-accepts-named-under-selection.md) |
| §4.2 | "It failed in the previous run" removed as a selection rule; four rules remain, and each selected test now records which of them selected it | [ADR-0001](docs/adr/0001-reach-persists-no-state-between-runs.md) |
| §4.2 | Rule 3, "new since the baseline", now states that newness is derived from the change set and depends on attribute-level change detection. Open, and listed as such in §11 | ticket 18 |
| §4.3 | Output is complete argument vectors per (test project, target framework) and a versioned report schema with a compatibility promise — not a `--filter` string and a five-field JSON blob | [ADR-0009](docs/adr/0009-per-host-private-filter-channels-over-runsettings.md) |
| §5 | Source-binary correspondence is verified from debug-symbol checksums in both build modes. v0.1's "freshness cannot be established, `--no-build` is the caller's risk" is superseded; the hazard paragraph became a description of what the check defends against | [ADR-0003](docs/adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md) |
| §8 | Analysis scope is the union of test-project closures, not every assembly in the solution — which is what makes a single-test-project target legitimate. The prohibition on changed-side narrowing is unchanged and stays prominent. New §8.2 | [ADR-0002](docs/adr/0002-analysis-scope-is-the-union-of-test-project-closures.md) |
| §9.1 | Cites `dotnet test --affected-tests` as a worked example of the rejected approach, authored by the .NET team | ticket 16 |
| §9.2 | The claim that IL exposes async state machines and generic instantiations as concrete methods is qualified: an async body is a method nothing in first-party IL calls, and a generic instantiation lives at the call site, not in the definition table | [ADR-0006](docs/adr/0006-a-graph-node-is-an-il-method-definition.md) |
| §11 | M0's build-to-test ratio measurement is recorded as consciously waived, not outstanding | owner, §11 |
| §11 | Two honest additions: coverage is more precise exactly where Reach is weakest, and "no infrastructure ask" is a narrower moat than §9.1 assumes, with a revisit trigger | ticket 16 |
| §11 | Data-driven test identity and "is standalone mode supported at all?" are marked settled rather than left open | tickets 01, 03 |
| §2, §4.1, §10 | Consistency with the two above: "emits a test filter" became "emits the commands", and §4.1's "every assembly in the solution's output" now points at §8.2 | ADR-0002, ADR-0009 |
| §12 | M1 restated to match the design: widening, edge provenance, three test frameworks across multiple dialects, multi-targeting, the fallbacks, the report contract and the limitations register. Whole-project fallback moved from M2 to M1. M1 gets its own correctness criterion, since shadow mode is M2's | this map |

Design decisions taken in the same period that did **not** need a PRD amendment — the walking
skeleton's project layout, the CLI surface, fixture strategy, the graph's internal design — are
in [docs/adr/](docs/adr/) and in the M1 spec.