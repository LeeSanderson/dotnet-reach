# Reach — Product Requirements

**Status:** Draft v0.1 · 3 September 2026
**Owner:** Lee Sanderson

---

## The bottom line, in plain terms

Large .NET codebases run their entire test suite on every pull request, even though most changes only affect a small part of the system. That wastes CI minutes and slows down feedback.

Reach is a command-line tool that looks at what a developer changed, works out which tests could possibly be affected by that change, and tells the build to run only those. It works out the answer by reading the compiled application — following the chain of "who calls what" backwards from the changed code until it arrives at a test — rather than by watching previous test runs.

The name is the question it answers: *what does this change reach?*

---

## How to read this document

Section 1 states the problem and who has it. Section 2 fixes the scope, which matters because "run fewer tests" is a wide idea and this document is only about one narrow way of doing it. Section 3 defines every term before it is used elsewhere. Sections 4 to 8 are the requirements. Section 9 records the decisions already taken and, more importantly, *why* — those reasons are the defence when someone proposes the obvious alternative. Sections 10 to 12 cover what is deliberately excluded, what is still unknown, and how we would know if this worked.

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

Reach answers exactly one question: *given a set of changed source files, which test methods in this solution could be affected?* It emits that answer as a test filter. It does not run the tests, own the pipeline, or reason across repository boundaries.

Things that are adjacent and deliberately outside this scope:

- Selecting which *services* to deploy after a change. Different question, different graph.
- Cross-repository impact analysis. A separate and much harder problem.
- Test quality, flakiness detection, or ordering.
- Anything in a language other than C#, F# or VB compiled to .NET IL.

---

## 3. Terms

Defined here so that later sections do not need to explain themselves.

**Test impact analysis (TIA).** The general practice of running only the tests affected by a change instead of the whole suite.

**Selection.** The set of tests Reach decides to run. **Over-selection** means running more tests than strictly necessary — wasteful but safe. **Under-selection** means failing to run a test that would have caught a regression — the failure mode that destroys trust in the tool and must be treated as a correctness bug, not a tuning issue.

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
3. **Build the call graph.** Read every assembly in the solution's output using `System.Reflection.Metadata`. Collect `call`, `callvirt`, `newobj` and `ldftn` targets. Build a type hierarchy index in the same pass so virtual and interface dispatch can be widened. Invert to get reverse edges.
4. **Apply framework models.** Each loaded model may add edges the IL does not contain.
5. **Walk backwards.** From the changed method set, traverse reverse edges to every reachable test method.
6. **Emit.** Produce a test filter expression, plus a machine-readable report of what was selected, what was not, and why.

### 4.2 Selection rules

A test is selected if any of the following hold:

- It is reverse-reachable from a changed method.
- Its own source changed.
- It is new since the baseline.
- It failed in the previous run (where that information is available).
- It lives in a project that Reach has declared unanalysable (see §8).

### 4.3 Output contract

Reach writes a filter suitable for `dotnet test --filter`, and a JSON report containing: the selected tests, the total test count, the changed methods that drove each selection, every framework detected without a model, and every project that fell back to coarse selection. The JSON is the audit trail; without it nobody can debug a surprising result, and a tool nobody can debug gets switched off.

---

## 5. Build handling

Reach analyses compiled assemblies, so compiled assemblies must exist and must correspond to the current source.

**Two CI modes, both explicit:**

- **Default.** Reach invokes `dotnet build` itself. On a tree that is already built this is a fast no-op, because MSBuild's own up-to-date checking is the freshness check — Reach does not implement its own. On a clean checkout it is a full build.
- **`--no-build`.** Reach trusts the output on disk. This is an assertion by the caller, documented as such, for pipelines where a previous step has just built the solution.

**If the build fails, Reach exits with an error and selects nothing.** There is no degraded mode. A failed build already fails the pipeline, so a clever test selection over a broken tree has no consumer.

**Known hazard, to be documented prominently:** file timestamps are not a trustworthy freshness signal on a warm CI agent or a cache-restored workspace, because git does not preserve modification times. Checking out an older commit onto a warm tree can leave source files older than the binaries beside them, and MSBuild will then correctly conclude nothing needs rebuilding while the binaries belong to a different commit. `--no-build` on a warm agent is the caller's risk to manage.

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

**The analysis scope is always every assembly in the solution.** Build scope may be narrowed; analysis scope may not. Reverse walks cross interface boundaries into projects that do not depend on the changed one — an implementation's callers usually reference only the interface — so a narrowed analysis silently under-selects. These are two different sets and conflating them is the most attractive available mistake.

**Shadow mode is a launch requirement, not a nice-to-have.** Reach must be able to run the selection, then run the whole suite anyway, and record every test it would have skipped that failed. No team should rely on the selection until that number has been zero for a meaningful period on their own codebase. Building and interpreting this harness is comparable work to the selector itself.

---

## 9. Decisions taken, and why

### 9.1 Static analysis, not coverage-based

Coverage-based selection is more precise: it records which implementation actually ran, so it never has to widen through an interface.

It was rejected because it requires standing infrastructure. The index cannot live in the repository — it would be a hot file generating constant merge conflicts — so in practice it lives in a service, keyed by commit. That means a main-branch build running the full suite under per-test instrumentation on every commit, storage serving indexes by commit, and a policy for cache misses, which are frequent because a long-lived branch's merge-base can be far behind.

Reach must be installable on an unfamiliar repository in an afternoon with no infrastructure ask. That single requirement rules coverage out.

The cost accepted: over-selection through interface dispatch, and blind spots requiring framework models.

### 9.2 Compiled IL, not Roslyn source analysis

`MSBuildWorkspace.OpenSolutionAsync` takes minutes on a large solution, which would consume the saving. Reading assembly metadata takes seconds. IL also exposes async state machines, lambdas and generic instantiations as concrete methods, and includes source-generated code without re-running generators.

Roslyn is still used, but only to diff the changed files — never to load a solution.

### 9.3 No persisted call graph in CI

Rebuilding from the binaries every run makes graph staleness impossible by construction, which removes an entire class of invalidation bug for a cost of a few seconds against a suite measured in tens of minutes. Local mode is the deliberate exception (§7).

### 9.4 Performance constraints this implies

Method identity must be integers derived from metadata tokens, never strings — the difference between seconds and minutes at solution scale, and painful to retrofit. The type hierarchy index must be built in a single pass; resolving implementations per call site is accidentally quadratic and will look fine on a sample repository and fail on a client's.

---

## 10. Non-goals

- Running tests. Reach emits a filter; the pipeline runs the tests.
- Replacing NCrunch. Reach selects after a build; NCrunch selects on an uncompiled edit.
- A hosted service, dashboard or account.
- Guaranteeing perfect selection. No test impact analysis is exact, and any documentation claiming otherwise is lying. Reach's claim is that it is conservative and that it says so when it cannot see.

---

## 11. Open questions and risks

**The build-to-test ratio is the viability test.** In a clean CI checkout the full build is unavoidable, so Reach only ever saves test time. A codebase that builds in six minutes and tests in twenty-five is a strong candidate. One that builds in twenty and tests in eight cannot be helped by any selection tool. *This should be measured on two or three real client solutions before further engineering.*

**Over-selection in layered DI codebases is the main technical risk.** Widening through interfaces in a codebase where everything sits behind one may select 80 to 90% of the suite, making the analysis pure overhead. Unknown until measured on a real solution, and it may vary enormously between codebases. A possible mitigation — narrowing widening using container registrations — is unexplored and would need care to avoid under-selection.

**Unmodelled-framework detection is harder than modelling.** Writing a MediatR model is an afternoon. Reliably detecting that a solution contains indirection nobody has modelled is fuzzy, and it is the feature that makes the tool safe.

**Data-driven test identity.** Theories and parameterised tests generate names differently per framework and sometimes non-deterministically. Proposed resolution: index and select at test-method granularity, never per test case. Accepts slight over-selection to remove a class of bug.

**Is standalone mode supported at all?** If Reach always owns the build, freshness never has to be inferred. Every scenario that needs `--no-build` should be enumerated before that flag ships.

---

## 12. Milestones and success criteria

**M0 — Viability check (days).** Measure build-to-test ratios on two or three real solutions. Kill or proceed.

**M1 — Walking skeleton (about two weeks).** Roslyn changed-method diff; IL call graph; reverse walk; filter output; JSON report; the default and `--no-build` modes. No framework models. Correct on a sample repository.

**M2 — Trustworthy (about a quarter).** Framework models plus loader; unmodelled-framework detection; whole-project fallback; shadow mode with miss-rate reporting. Run in shadow on a real codebase for a month.

**M3 — Local mode.** Only after M2, and only if the `dotnet watch` spike shows the latency is worth pursuing.

**Success criteria:**

- **Correctness:** zero missed failures in shadow mode over a month of real use on a real codebase. This is a gate, not a target.
- **Saving:** median PR selects under 40% of the suite on a representative solution. Below that, over-selection has eaten the value.
- **Adoption cost:** a competent engineer can add Reach to an unfamiliar pipeline in under an hour, using only documentation.
- **Debuggability:** any surprising selection can be explained from the JSON report without rerunning the tool.

---

## Appendix — naming

**Reach** was chosen because reverse reachability is literally the algorithm, and "what does this change reach?" is the question a developer is actually asking. It is short, works as both verb and noun, and reads naturally as a command: `dotnet reach select`, `dotnet reach watch`.

Package name would be `dotnet-reach`, invoked as `dotnet reach`. **Availability on nuget.org has not been verified and must be checked before this is committed to.**

Alternatives considered: *Sift* (clear, slightly bland), *Winnow* (evocative, obscure), *Blast Radius* (matches the consultant's phrase but sounds destructive), *Ripple* (clashes with an older .NET tool).
