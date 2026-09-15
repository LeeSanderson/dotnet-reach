# Rendering and delivery

Status: resolved
Depends on: 11, 12
Spec: [§12.3](../../walking-skeleton/spec.md#123-rendering)–[§12.6](../../walking-skeleton/spec.md#126-exit-8-is-an-instrument-not-a-hazard) · [ADR-0009](../../../docs/adr/0009-per-host-private-filter-channels-over-runsettings.md), [ADR-0015](../../../docs/adr/0015-invocations-pin-the-target-framework-where-it-is-derivable.md)

## Goal

Turn the canonical selection into argument vectors a pipeline can execute — correctly, for every
**dialect**, at every length, without ever rendering a filter that matches nothing.

## Scope

**M1 renders for `dotnet test` only**, and this is not a gap left open: it is what keeps the
exit-8 claim below honest.

The **runner host** is not a property of a test project — the same assembly answers to
`dotnet test`, to its own executable, and for some package versions to VSTest. Reach emits an
argv, so **the argv already is the choice of host**, and `dotnet test` is the one choice
determinable from the assembly: the generated entry point dispatches on `--internal-msbuild-node`,
which `dotnet test` always passes, so it is MTP regardless of `UseMicrosoftTestingPlatformRunner`.

**Do not also render the native dialect.** The two hosts' option-name sets intersect in exactly
four names — `culture`, `debug`, `explicit`, `filter` — and MTP normalises a single dash to a
double, so **`-filter` is accepted by both hosts in different filter languages with no diagnostic
either side**. Rendering both would manufacture the exact artifact that makes exit 8 ambiguous.

**Three rules the renderer must implement.**

**1. NUnit renders with `~` (contains), never equality.** NUnit's VSTest `FullyQualifiedName`
*includes* a parameterised test's arguments — `Ns.C.MyTest(1,2)` — so
`FullyQualifiedName=Ns.C.MyTest` ought to match nothing, and works only because the adapter
re-parses the filter into NUnit filter XML where matching is parent-aware. That chain breaks under
`UseNUnitFilter=false`, under `DiscoveryMethod.Legacy`, and on the IDE path. **Reach cannot see a
consumer's `.runsettings` and cannot detect the failure either** — an under-selected theory simply
does not run — so this follows widen-when-uncertain. Errs over, `nunit-filter-overmatch`.

**2. Every invocation for a project with more than one assembly instance carries
`-f <moniker>`.** `dotnet test` runs every target framework of a project, so a filter naming a
test that exists under only one of them makes the others exit 8 — **Reach would render the
*correct* answer and fail the build with it.** A single-instance project has nothing to
cross-contaminate and gets no selector, which matters more than it sounds: a single-targeted
`net10.0-windows` WPF or WinForms suite never needs a moniker and never degrades.

Moniker derivation is **metadata-only**:

- `.NETCoreApp,Version=vN.M` → `netN.M`; `.NETStandard,Version=v2.0` → `netstandard2.0`.
- **No `TargetPlatformAttribute` → no suffix, and the moniker is exact.**
- **`TargetPlatformAttribute` present on a multi-instance project → stop.** The declared moniker
  is unrecoverable: the attribute always carries a version, so `net10.0-windows` reads back as
  `net10.0-windows7.0` — which `-f` rejects, with an error naming the wrong subsystem entirely —
  and a project declaring `net10.0-windows7.0` produces byte-identical attributes, so **no
  reconstruction rule can be correct for both**. Emit one project-wide `run-all` invocation with
  no selector and report `framework-selector-underivable`. Errs over, registered, with
  `dotnet msbuild -getProperty:TargetFrameworks` as the upgrade path.

**Rejected, and do not revisit without reading ADR-0015**: the output directory name, which *is*
the exact declared moniker under the default layout but reintroduces the path-trust ticket 07
spent its whole resolution removing; and emitting the reconstructed moniker and accepting the
risk, which ships a command known to be broken.

**3. An emitted `dotnet test` argv must never carry `-o`.** MTP-mode `dotnet test` has **no `-o`
at all** and spells `--output <Minimal|Normal|Detailed>` to mean *test output verbosity*, while
VSTest-mode `dotnet test` and `dotnet build` use `-o|--output` for a directory.

**Delivery. The length ceiling is the normal path, not an edge case.** A
`FullyQualifiedName=…` clause runs 60–90 characters, so `cmd`'s 8,191 limit arrives at roughly
**100 selected test methods** and `CreateProcessW`'s 32,767 at roughly **400**. An ordinary pull
request on a large solution clears both.

| Host | Channel |
|---|---|
| Microsoft.Testing.Platform | `@file.rsp` |
| xUnit v3 native CLI | `@@ file` |
| nunit-console | `--testlist=FILE` |
| **VSTest under `dotnet test`** | **none — chunk across multiple invocations** |

All three channels are **Reach's own files, passed as arguments**: they occupy no channel the
consumer configures, cannot merge with consumer state, cannot be silently intersected, and carry
no length limit worth budgeting for.

**Do not default to `.runsettings` `<TestCaseFilter>` despite it having no length limit at all**
(maintainer-tested at 3.5 MB). Two independent disqualifiers: a pre-existing `<TestCaseFilter>` is
**AND-ed** with Reach's, and a consumer's filter is typically an exclusion, so the intersection is
strictly smaller than the selection — an **undetectable silent under-selection**; and only one
settings file can be passed at all, so a file Reach writes *replaces* the consumer's. Reach is
invoked as a separate step that never sees the runner's arguments, so detection cannot rescue
either.

**`--runsettings <path>` is the explicit opt-in**: the caller supplied the fact Reach could not
discover, so Reach may merge and emit a combined file. If that file already carries a
`<TestCaseFilter>`, **refuse to render a filter for that project and downgrade it to `run-all`**
with `runsettings-filter-conflict`. Errs over.

`vstest.console.exe` invoked directly *does* accept `@file` and is rejected: locating it means
discovering a Visual Studio installation or a test-platform package layout, a larger problem than
the one it solves.

**Side-car files** live in the Reach-owned directory, named deterministically from project + TFM +
chunk index so a re-run overwrites rather than accumulates, with **absolute paths in the argv** so
a pipeline that changes directory between steps still works. **Never** a path MSBuild or Visual
Studio reads by convention — no `Directory.Build.props`, no project-root `.runsettings` — and
never the system temp directory, which agents clear between steps and which is the least
inspectable place to look when a filter misbehaves. Every chunk and every downgrade emits a
notice.

**Both counts.** The canonical selection and the rendered filter deliberately disagree, because of
rule 1. Compute the rendered filter's **true match set** by applying the dialect's own matching
semantics back over the enumerated test list — cheap, since ticket 11 already enumerates every
test method, and for M1 only NUnit over-matches, so it is one substring pass. Emit
`dialect-over-selects` when non-zero. **Ticket 18 would otherwise measure the wrong number.**

**Empty and degenerate selections.** An empty selection is `mode: skip` with `invocations: []` —
never an empty filter string, which runs everything. **`run-all` with zero invocations is also
representable**, from the underivable-moniker case: the single project-wide invocation is carried
by the **first entry in the report's own sort order** and the rest carry empty arrays, so a
consumer's loop issues exactly one command. A consumer reading one entry in isolation must
therefore not infer "nothing to run" from an empty array alone — `mode` is the field that says it.

**Reach never emits `--ignore-exit-code 8`.** It does not merely suppress the signal, it erases
the evidence: the zero-match module's line changes from *Zero tests ran* to *passed* and the
annotation disappears from the summary entirely. With the selector in place **no path through
Reach's design renders a filter matching nothing**, which makes exit 8 from a Reach invocation
mean a Reach rendering bug and nothing else.

## Acceptance criteria

- A golden argv test per (framework, package version, host) combination in the recognition table.
- **NUnit renders `~`, not `=`** — asserted directly, since equality is what anyone would write
  first.
- The rendered-match count reports a `MyTest2` extra for a `MyTest`/`MyTest2` pair, and
  `dialect-over-selects` fires.
- **The chunker**: feed it 200 test identities and assert the invocations partition the set with
  nothing dropped and nothing duplicated. A unit test — a hundred-test fixture solution to prove
  list partitioning is a bad trade.
- The ceiling is detected per platform, never read from a flag.
- **Every emitted argv carries `-f` matching its entry's target framework where the project has
  more than one assembly instance**, and none where it has one. A unit assertion: a missing
  selector fails loudly at run time (exit 8, offending module named), so it does not need a
  fixture.
- A multi-instance project carrying `TargetPlatformAttribute` yields one project-wide `run-all`
  invocation on the first entry in sort order, empty arrays on the rest, and
  `framework-selector-underivable`.
- **No emitted argv contains `-o`.**
- A `--runsettings` file with a `<TestCaseFilter>` downgrades to `run-all` with a notice; one
  without merges.
- Side-car paths in the argv are absolute, and re-running overwrites rather than accumulating.
- A selection containing names with commas, parentheses, backticks, generic arity and nested-type
  separators renders and round-trips through the dialect's escaping.

## Out of scope

The direct-executable host and VSTest-native rendering — out of scope for M1 with **no notice**,
since it would fire on nearly every xUnit v3 project and mean nothing. MTP test-node UIDs — out of
scope; UIDs come from a discovery pass Reach does not run.

## Comments

**Implemented** in `Reach.Core/Rendering`: `FilterDialect`, `CommandLineCeiling`, `Chunker`,
`FrameworkSelector`, `RunSettings` and `Renderer`. The report's `invocations`, `delivery` and
`willRun` now come from it.

**Stage A is complete.** Pointed at its own repository, Reach selects tests for the working
tree's changes, writes a response file, and emits an argv that runs:

```
Selected 197 test(s).
  Reach.Tests (net10.0)  filtered  197/336

"invocations": [["dotnet","test",".../Reach.Tests.csproj","--configuration","Release",
                 "@.../.reach/Reach.Tests.net10.0.0.rsp"]]
```

Running that command by hand: 251 cases, all passing. 197 methods against 251 cases is the
parameterised-test expansion, and is why selection granularity is the method.

**One rule had to be read at project scope rather than instance scope.** The ticket says a
`TargetPlatformAttribute` on a multi-instance project degrades it to one project-wide
invocation. The first implementation checked the *current* instance, so a
`net10.0`/`net10.0-windows` pair rendered a pinned filter for `net10.0` and degraded only the
windows half — which is wrong, because `dotnet test` runs every target framework: one sibling
that cannot be pinned makes every sibling's filter reach it, and pinning the others correctly
would not save the run. Underivability is a property of the project. Named test.

**`--configuration` is on the emitted argv when the caller gave one**, which the ticket does
not mention. The same reasoning that passes it to the build applies here: a selection computed
over Release output that the runner then executes against Debug is a different set of tests.
`-o` and `--output` are never emitted, asserted directly.

**Delivery is chosen by host, from the dialect.** `xunit-v3*` answers to Microsoft.Testing.
Platform and gets a response file; everything else under `dotnet test` has no private channel
and gets chunks. That follows the ticket's table. A project that opts NUnit or MSTest into MTP
would be chunked unnecessarily — over-delivery, never a wrong selection, and not worth a
detection rule until something shows it matters.

**`willRun` is computed against every enumerated test**, which is why `ProjectSelection` now
carries `AllTests`. Computing it against the selection alone would have made the `~` dialect's
over-match invisible, and the over-selection measurement would read the wrong number — the one
thing this half of the ticket exists to prevent.

**Escaping is asserted character by character**, including a name carrying all nine grammar
characters at once, with the assertion counting *unescaped* separators rather than splitting
naively — the first version of that test split on `|` and failed on its own escaping.

**The consumer's half is driven, not inferred.** `ConsumerLoopTests` loops over a report's
`invocations` with a fake runner and asserts that an empty selection and a `no-changes` report
each start no process, and that a real selection starts exactly one. That is spec §16.2's
second mandatory assertion.

Not covered here, and correctly so: a golden argv per *runner host* is one row wide, because M1
renders for `dotnet test` only — which is what keeps the exit-8 claim honest rather than a gap
left open.
