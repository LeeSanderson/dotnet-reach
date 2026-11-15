# Acceptance layer

Labels: wayfinder:map

## Destination

A **spec for Reach's acceptance test layer**, plus implementation tickets handed off to a
separate implementation effort — the same shape as `walking-skeleton/` → `m1-implementation/`.

Planning only. This map produces decisions and a spec, not code. When a ticket makes you want
to start building, that is the edge of the map: hand off instead.

## Notes

- Product definition: [PRD.md](../../PRD.md). Vocabulary: [CONTEXT.md](../../CONTEXT.md). The
  M1 spec is [walking-skeleton/spec.md](../walking-skeleton/spec.md); today's acceptance tests
  came from [The fixture solution and the acceptance assertions](../m1-implementation/issues/19-fixture-solution-and-acceptance.md).
- Default skills for a ticket: `grilling` and `domain-modeling`, unless its `Type:` says
  otherwise.
- **The release is on hold for this map.** [Publishing](../m1-implementation/issues/21-publishing.md)
  carries an `On hold:` line; no `v*` tag is pushed until this map's destination is reached.
- **Vocabulary for this effort.** Test-infrastructure terms, so they live here and in the spec,
  not in `CONTEXT.md` (which is Reach's product glossary):
  - **Sample solution**: a buildable .NET solution that Reach is pointed at. Not "sample
    project" — a *project* is one `.csproj`, and a sample needs several.
  - **Sample test**: a test method inside a sample solution. Something Reach *selects*, never
    something that checks Reach.
  - **Patch**: a change applied to a sample solution's code.
  - **Acceptance test**: one patch plus its expected result, run as a test in Reach's own suite.
  - **Known-gap test**: an acceptance test that pins a documented imprecision (over- or
    under-selection) rather than a claim. Named as a gap; never in the release gate.
  - **Release gate**: every claim the alpha's docs make, each proven by some suite.
- **Reach's output is not changed to suit the acceptance layer.** Acceptance tests read
  `report.json` as Reach already writes it.
- **Facts gathered while charting** (from a survey of the suite on 2026-11-14): ~440 tests in
  one project; 15 run a real `dotnet build`, all over `tests/fixtures/Contoso/`, two builds per
  test with no sharing (the class-fixture comment at `tests/Reach.Tests/Fixtures/FixtureSolution.cs:17`
  is stale); no test runs the sample tests themselves; no patch format exists — changes are
  inline find/replace (`Touch`) or whole-file writes; no slow-test trait, the acceptance group is
  filtered by namespace. About 25 in-memory tests assert change → selected tests
  (`Selection/SelectionTests.cs`, `WholeProjectAndWideningTests`, `AcceptedOverSelectionTests`).

## Decisions so far

Taken while charting, before any ticket existed. Terse by design.

- **Three jobs at once**: the layer is the release gate for `0.1.0-alpha.1`, a regression net
  that grows with each feature and bug, and a showcase of what Reach handles.
- **The oracle is an exact expected selection**, written by hand per acceptance test. The
  sample tests are never run to check Reach. An exact match catches over-selection too, so
  unselected tests are implied, not listed.
- **The expected result covers** the selected sample tests, the outcome (in `report.json`'s
  words: `selected` · `nothing-selected` · `no-changes` · `failed`) and the projects run in
  full by whole-project fallback. ~~Not notices, not tiers~~ — *widened by the capabilities
  ticket below: optional will-run, notices, changes and tier sections, each exact when given.*
- **Two kinds of sample solution**: small ones built for one area of capability, and
  multi-project ones shaped like a real codebase, where most test projects must *not* be
  selected.
- **It replaces today's Contoso acceptance tests** (11, not the 13 first counted); Contoso becomes the first small sample
  solution. The in-memory tests stay where they are.

From tickets:

- [How a patch and its expected result read](issues/01-patch-format-and-readability.md): a
  plain unified diff (`---`/`+++` and labelled `@@` hunks only, no `index` lines), applied
  with `git apply`, never `--3way`.
- [What does a built, patched sample solution cost?](issues/02-building-a-patched-sample-cheaply.md):
  no baseline build is needed. One build of the patched tree is 4–6 s per test. Copying built
  trees buys nothing. Parallel runs with one directory per test are 2.3–2.7× faster.
- [Which capabilities the layer demonstrates, and which gate the release](issues/03-capabilities-and-the-release-gate.md):
  27 rows — 16 gate capabilities on small samples, 7 on the multi-project one (each about what
  is *not* selected), 4 known-gap tests outside the gate. The gate is every claim in the docs,
  proven by *some* suite; acceptance tests stay in-process. The expected result widens to
  will-run, notices, changes and tier; every test checks determinism.

## Not yet specified

- **A second multi-project sample solution?** — whether rows 17–23 and 26–27 fit one, once the
  first is designed. The small ones graduated into [The small sample solutions](issues/10-the-small-sample-solutions.md).
- **Writing the spec and cutting the implementation tickets** — the last ticket, once the
  frontier above has cleared. The spec carries the claim-to-suite table.
- **What happens to each claim no suite proves** — test it or cut it from the docs, once
  [Which suite proves each claim](issues/11-which-suite-proves-each-claim.md) lists them.

## Out of scope

- **Running the sample tests to check Reach's selection** — ruled out while charting: the
  oracle is the expected selection.
- **Moving the in-memory selection tests into sample solutions** — ruled out while charting;
  they stay fast and isolated. A sample solution may *repeat* their cases for the showcase.
- **Changing Reach's output for the acceptance layer's benefit.**
- **Publishing `0.1.0-alpha.1`** — ticket 21 of `m1-implementation`, on hold until this map
  is done.
