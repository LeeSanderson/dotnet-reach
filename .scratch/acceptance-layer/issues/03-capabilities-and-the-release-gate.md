# Which capabilities the layer demonstrates, and which gate the release

Type: grilling
Status: resolved
Blocked by: (none)

## Question

The layer has three jobs — release gate, regression net, showcase. That needs a list:
**which of Reach's capabilities must have an acceptance test**, and **which subset must pass
before `0.1.0-alpha.1` is tagged**?

Candidates come from the spec and the register, not from invention: compiled edges across
assembly boundaries, widening (interface and virtual dispatch), whole-type and whole-assembly
widening, recompilation widening (`const`, removed overloads), project-file changes and
`Directory.Build.props` containment, test recognition per framework, whole-project fallback,
the empty selection, the NUnit `~` over-match, multi-targeting.

For each: is it shown in a **small sample solution** or a **multi-project one**, and is it in
the **release-gate batch** or a later one? The answer seeds the design of the first
multi-project sample solution.

## Answer

Settled with the owner over four rounds of grilling, from an inventory of every behavioural
claim in `README.md`, `docs/adopting-reach.md` and `docs/report-schema.md`.

### The rules

- **The release gate is every claim the alpha's docs make.** A claim we will not gate on comes
  out of the docs instead of staying unproven.
- **A claim may be proven by any suite, not only this one.** The acceptance layer proves the
  claims about selection. The existing CLI tests (`Cli/ExitCodeTests.cs`,
  `Cli/CommandLineTests.cs`, `Assemblies/CorrespondenceInBothModesTests.cs` and others) cover
  the rest. **The spec carries a table of claims and the suite that covers each one**, so a gap
  is visible.
- **Acceptance tests run Reach in-process**, the way `FixtureSolution` does today, and not
  through the CLI. So an expected result has no exit code.
- **Small or multi-project is set by what the patch proves.** If the point is that something
  *is* selected, the test uses a small sample solution. If the point is that something is *not*
  selected, it uses the multi-project one, because only that one has tests to leave out.
- **Known gaps are acceptance tests too.** A known-gap test pins a deliberate over-selection, or
  a documented under-selection, with an exact expected result. It is named as a gap (for
  example `Known_gap_interpolated_ToString_is_not_selected`), so nobody reads it as a claim. It
  turns red when the gap closes. **Known gaps are never in the release gate.**

### The expected result is wider than the map said

This **reopens a decision taken while charting** ("not notices, not tiers"). An expected
result may now also give:

- **will run**: the rendered filter's true match set. The NUnit `~` over-match needs it.
- **notices**: the codes.
- **changes**: each changed member with its test count.
- **tier**: given on each change. Without it, whole-assembly widening looks the same as a call
  graph that happens to reach everything (see the `widenedPairs` entry in the register).

Each of these sections is optional. **A section that is given is exact**, so a notice nobody
expected fails the test. A sample solution must therefore be built so that it does not trip
notices it does not mean to.

**Determinism is checked on every acceptance test.** Reach runs twice and the two
`report.json` files must be byte-identical. Whether that cost is acceptable goes to
[How the harness builds each acceptance test](09-how-the-harness-builds-each-acceptance-test.md).

### Recognised dialects

There is one small "frameworks" sample solution. It has one production project and a test
project for each of `xunit-v2`, `xunit-v3`, `xunit-v3-4`, `nunit`, `mstest` and one
unrecognised framework. Every test project calls the same production code.

### The capability list

**Release gate, small sample solutions**

| # | Capability | Shown by |
|---|---|---|
| 1 | A change selects exactly the tests that reach it through compiled edges | selection |
| 2 | A test whose own source changed is selected | selection |
| 3 | A test that is new since the baseline is selected | selection |
| 4 | Interface or virtual dispatch selects every candidate implementation | selection, `widened` |
| 5 | A deleted method widens its declaring type | tier `whole-type` |
| 6 | A move to another directory, in the same namespace, widens that type | tier `whole-type` |
| 7 | Each dialect is recognised and renders its own filter; NUnit's `~` runs more tests than it selects | will run, notices |
| 8 | A test project on an unrecognised framework runs in full, with an unknown total | run in full, notices |
| 9 | A multi-targeted test project gives one entry per framework, each pinned with `-f` | selection per framework |
| 10 | A platform-suffixed framework makes its project run in full | run in full, notice |
| 11 | xunit.v3 4.0.0 without the runner setting gets its notice | notices |
| 12 | A generator project is in scope, and a change to it widens its consumers | selection |
| 13 | A change to formatting only gives `nothing-selected` | outcome |
| 14 | A change to docs only gives `no-changes` | outcome |
| 15 | A changed member that reaches no test is listed with a count of 0 | changes |
| 16 | A new C# file that is not tracked by git is flagged | notices |

**Release gate, multi-project sample solution.** Each of these is about what is *not*
selected.

| # | Capability |
|---|---|
| 17 | A cross-assembly edge selects the consumer's tests and no others |
| 18 | A changed `const` widens only its transitive referencers (`recompilation-widening`) |
| 19 | A removed member widens only its transitive referencers |
| 20 | A `.csproj` change widens only its assembly and the assemblies that depend on it |
| 21 | A `Directory.Build.props` change widens only the projects beneath it |
| 22 | A move to another namespace widens the whole assembly, and nothing further |
| 23 | A root file that matches no rule selects nothing (`unmapped-file-no-project`) |

Row 23 is the register's only *deliberate* under-selection. It is in the gate because the docs
state it as intended behaviour.

**Known gaps, not in the gate**

| # | Gap | Sample solution |
|---|---|---|
| 24 | Generic instantiations are collapsed: `Handler<Foo>` also selects the users of `Handler<Bar>` | small |
| 25 | An interpolated `ToString` is not selected | small |
| 26 | An interface-typed receiver (the DI shape) fans out to every implementer | multi-project |
| 27 | Recompilation widening's blast radius: a core `const` widens most of the solution | multi-project |

**Left to other suites**: exit codes, argument parsing, `--no-build` and exit 5,
`--runsettings`, `--paths`, CI baseline detection, shallow clones, response-file and chunked
delivery, and `.reach/.gitignore`.

### Facts found on the way

- **There are 11 Contoso acceptance tests, not 13.** They run through
  `ReachPipeline.SelectAsync` with `NoBuild = true` after a rebuild of their own, so default
  build mode is never exercised end to end.
- **No fixture has these shapes**: an interface or virtual dispatch (needed for `widened`), a
  multi-targeted *test* project, a platform-suffixed framework, or a `.csproj` or
  `Directory.Build.props` change. **No test anywhere asserts `changes[].tier`.**
