# The small sample solutions

Type: grilling
Status: resolved
Blocked by: (none)

## Question

[Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md)
puts rows 1–16 (gate) and 24–25 (known gaps) on **small sample solutions**, and fixes one of
them: a "frameworks" sample with a test project per dialect (`xunit-v2`, `xunit-v3`,
`xunit-v3-4`, `nunit`, `mstest`, unrecognised) over one production project.

How do the rest group into samples? How many, what each holds, and which rows each carries.
Does **Contoso become the frameworks sample**, or a different one, or retire?

Constraints carried from that ticket:
- An expected section that is given is exact, so a sample must not trip notices it does not
  mean to. Row 11 (the runner-setting notice) and a clean dialect row may not share a sample
  without listing the notice everywhere.
- Shapes no fixture has yet: interface/virtual dispatch, a multi-targeted *test* project, a
  platform-suffixed framework.
- Each sample costs one build per acceptance test (4–6 s, from
  [What does a built, patched sample solution cost?](02-building-a-patched-sample-cheaply.md)).

## Answer

Settled with the owner over four rounds of grilling (2026-11-17).

### The rule that shaped it: no standing notice a test does not mean

Four notices fire on **every** run of a sample that has their shape, whatever the patch:
`nunit-filter-overmatch` (any NUnit project), `whole-project-fallback` (any unrecognised
project), `framework-selector-underivable` (a multi-targeted project with a platform suffix) and
`test-runner-not-configured` (an `xunit.v3` 4.0.0 project with no runner setting in
`global.json`). Since a given notices section is exact, a sample holding one of these makes every
test on it list that notice. **So a shape that brings a standing notice goes either on a sample
whose point it is, or behind a baseline patch.**

### Baseline patches (new)

A **baseline patch** is applied to the copied sample **before** the baseline commit, so it
becomes part of the baseline and Reach never sees it as a change. The **change patch** is applied
after the commit, as today. One sample can then carry variants of its starting state without a
directory per variant, and without the variant's edit becoming a second claim in the expected
result. (A `.csproj` edit made as a change patch widens its assembly; deleting `global.json` as a
change patch widens the whole solution. Both would turn a one-claim test into two.)

- **At most one baseline patch per test.** A change patch is written against the untouched sample
  or against exactly one baseline patch. "Independent" now means: each *(baseline patch, change
  patch)* pair applies to the untouched sample by itself.
- Declaration, by sketch: `Baseline("no-global-json.patch").Apply("x.patch").Expect(...)`.
- Considered and taken over separate sample directories, at the cost of three reopened decisions
  (see Amends below) and a starting state the reader must assemble from the sample plus one diff.

### The samples

| Sample | Rows | Shape |
|---|---|---|
| **Basics** | 1, 2, 3, 5, 6, 11†, 12, 13, 14, 15, 16 | one production project, one `xunit.v3` 4.0.0 test project, the generator project, `global.json` with the runner setting |
| **Frameworks** (was Contoso) | 7, 8 | one production project; test projects for `xunit-v2`, `xunit-v3` (3.x), `xunit-v3-4`, `nunit`, `mstest`, unrecognised; `global.json` |
| **Dispatch** | 4 | interfaces and virtual methods |
| **MultiTarget** | 9, 10† | one test project on `net8.0;net10.0` |
| **GenericCollapse** | 24 (known gap) | `Handler<Foo>` / `Handler<Bar>` |
| **InterpolatedToString** | 25 (known gap) | an interpolated `ToString` |

† via a baseline patch:
- **Row 11**: a baseline patch on Basics deletes `global.json`; any one-line body change is the
  change patch. Basics' `global.json` therefore holds the runner setting and nothing that changes
  how the sample builds (no SDK pin), so deleting it alters only the notice.
- **Row 10**: a baseline patch on MultiTarget changes the test project to
  `net10.0;net10.0-windows`. It must also set `<EnableWindowsTargeting>true</EnableWindowsTargeting>`,
  or the Linux CI leg fails with `NETSDK1100`. Row 9's test runs on the unpatched sample and lists
  no notice.

Further rules:
- **Every sample has `global.json` with the runner setting**, so no test expects
  `test-runner-not-configured` except row 11's. Owner's call over the leaner "only where an
  `xunit-v3-4` project exists".
- **Each small known gap gets its own sample**, so closing a gap touches one sample and one test.
  The multi-project known gaps (26, 27) stay on **Ledger**, which they need to exist at all.
- **Sample names say what the sample shows.** Ledger is the one domain-named sample.
- **Rows 5 and 6** (whole-type tier from a deleted method, and from a same-namespace directory
  move) are change-detection rows and sit on Basics, not Dispatch.

### Contoso becomes Frameworks

Renamed to **Frameworks** and moved to `tests/samples/Frameworks/`. It **gains** `xunit-v2`,
`xunit-v3` 3.x and MSTest test projects and a `global.json`. It **loses** `Contoso.Core`'s
multi-targeting (row 9 is about a multi-targeted *test* project, and lives on MultiTarget) and its
generator project (row 12 moves to Basics, so a generator change shows a readable selection rather
than a six-project fan-out). Its NUnit project keeps the parameterised test and the
`MyTest`/`MyTest2` pair that row 7's will-run count depends on.

### Amends

- [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md): `Apply(...)`
  gains an optional baseline patch step.
- [Writing a patch and keeping it applying](08-writing-a-patch-and-keeping-it-applying.md):
  independence is per pair; `save` and `apply` must be able to work on top of a baseline patch.
- [How the harness builds each acceptance test](09-how-the-harness-builds-each-acceptance-test.md):
  the baseline patch is applied before the baseline commit.
- [Migrating the Contoso tests](06-migrating-the-contoso-tests.md): unblocked; Contoso is
  Frameworks, and its multi-targeting and generator move away.

### Not changed

No `CONTEXT.md` change and no ADR: test infrastructure, reversible. **Baseline patch** and
**change patch** join the map's vocabulary.
