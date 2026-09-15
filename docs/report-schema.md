# The report schema

`report.json` is Reach's product surface. A selection nobody can audit gets switched off, so
this document exists to make a surprising one explicable **without rerunning the tool** — which
is also why there is no `explain` verb.

[`example-report.json`](example-report.json) is a real report, and is the byte-for-byte output
of the test suite's determinism fixture. A test fails the moment the two disagree, so the
example cannot rot.

## The compatibility promise

`schemaVersion` is a single integer. This release emits `1` and cannot emit any other.

**Your obligation as a consumer is part of the contract: tolerate unknown fields, unknown enum
members and unknown notice codes.** That tolerance is what makes new codes, kinds and outcomes
additive rather than breaking — without it, every gap Reach learns to detect would be a breaking
change, which would strangle the design.

Breaking means removing a field, repurposing one, or changing its type. `toolVersion` is
separate on purpose: a bug fix is not a schema change.

There is deliberately **no JSON Schema file**. The contract is tolerant, so a schema document
would have to be permissive everywhere the promise lives, and it would become a second artifact
to hold in sync with the code. It gets revisited when a consumer asks to validate, or at the
first `schemaVersion` increment.

**The report is byte-deterministic** for a given input. Every array is sorted by a documented
key: projects by path, tests by fully-qualified name, changes by display form, notices by code
then locator. Exclude `envelope.timings` and two runs over the same input are identical bytes.

A report is written for every outcome **including a failure** — a tool that writes nothing when
it fails is one you debug by re-running it with more flags, on CI. The one exception is a usage
error (exit 1), whose arguments were never valid enough to establish where to write one.

## Top level

| Field | Notes |
|---|---|
| `schemaVersion` | `1` |
| `outcome` | `selected` · `nothing-selected` · `no-changes` · `failed` |
| `reasons` | notice codes, present and non-empty **exactly when** `outcome` is `nothing-selected` |
| `envelope` | the run's own inputs — see below |
| `scope` | test projects, and in-scope assemblies as identities. Never closure edges |
| `summary` | the over-selection measurement. Absent when the run failed before rendering |
| `changes` | every change, with its tier and the count of tests it reached |
| `entries` | one per (test project, target framework) |
| `notices` | one global array, with locators in `data` |

**`no-changes` and `nothing-selected` are different news.** They exit the same and mean opposite
things: a docs-only pull request, against code changes that reached no test — which is either a
coverage gap or a bug in Reach.

On `no-changes` the full project list is still emitted, every entry `skip` with zero
invocations, so your loop behaves identically across all four outcomes.

## `envelope`

| Field | Notes |
|---|---|
| `toolVersion` | the Reach that produced this |
| `baseline` | `sha`, `reference`, `detectedFrom`, `isHead` — **resolved to a SHA**, not left as `origin/main`. Absent on exit 4 |
| `head` | the commit the working tree is on. Absent when git could not be read |
| `changeSources` | which of committed, staged, unstaged and untracked were read |
| `buildMode` | `build` or `no-build` |
| `forwardedBuildArguments` | everything after `--`, verbatim |
| `correspondence` | `verified` · `skipped` · `failed` |
| `timings` | `startedUtc`, `totalMs`, and Reach's own phases |

`baseline` earns its place alone: **the empty-change-set trap *is* a baseline that quietly
resolved to `HEAD`**, and `isHead` is how you catch it. Timings are segregated into one object
so that excluding a single key makes two reports comparable.

## `entries`

One per **(test project, target framework)**, uniformly, even for a single-targeted project. The
target framework is part of method identity, so two frameworks of one project can legitimately
select differently — collapsing them would force a union and destroy the answer to *"why did
`net8.0` run this?"*. A pipeline that prefers one invocation can coalesce; it cannot un-union.

| Field | Notes |
|---|---|
| `project`, `targetFramework` | the key. Always present |
| `framework`, `dialect` | the recognised test framework, and the filter grammar it renders into. **Both absent when the framework is unrecognised** — which is the case the rest of the entry describes. In this release they carry the same string: the dialect is gated on package version as well as framework, because `xunit.v3` 4.0.0 changed the filter surface, so it is the more specific of the two and is what both fields report |
| `packageVersion` | **reserved. Not emitted by this release**, so always absent |
| `runnerHost` | `dotnet-test`, or absent on `skip`. Present on a project run in full |
| `mode` | `filtered` · `run-all` · `skip` |
| `delivery` | `inline` · `response-file` · `chunked` · `run-settings`, or **absent** on an entry that carries no command of its own — see below |
| `counts` | `selected`, `willRun`, `total` |
| `tests` | the selected tests, enumerated |
| `invocations` | **an array of argv vectors** |

### `invocations` is always an array

Many when chunked, one for `run-all`, and **zero for `skip`**. That is the load-bearing part: a
consumer iterating blindly does nothing for an empty selection, so *"emit no command at all"* is
the only **representable** answer rather than a rule to remember. Two opposite failure modes are
closed by the same fact — an empty filter string runs everything, and an empty-match filter exits
8 and turns a green build red.

Argv vectors, never shell strings. There are already three escaping layers — the filter grammar,
MSBuild and XML — and a shell would be a fourth nobody should own.

**A `run-all` entry can also carry zero invocations.** When a multi-targeted project's declared
moniker cannot be recovered, one project-wide invocation is carried by the first entry in sort
order and the rest carry empty arrays — and those carry no `delivery` either, because they
deliver nothing. Your loop issues exactly one command. **Do not infer "nothing to run" from an
empty array alone — `mode` is the field that says it.**

### `counts.total` can say `unknown`

A project on a framework Reach does not recognise cannot be enumerated. It reports the string
`"unknown"`, never `0` — reporting zero would silently corrupt the measurement in exactly the
case where Reach is running an entire project.

**On that entry `willRun` reads `0` while every test in the project runs.** It cannot read
anything else: the count is unknown, and inventing one would be worse. So `willRun` is the
tests Reach can *account* for, not the tests that will execute — do not sum it across entries and
call it a total. `summary.projectsRunInFull` is how many entries are in this state.

`selected` and `willRun` deliberately disagree where the dialect matches by containment: see
`nunit-filter-overmatch`.

## `tests`

| Field | Notes |
|---|---|
| `display` | the fully-qualified test method name |
| `id` | stable within a run, comparable across runs of the same tool version |
| `rules` | why it was selected: `reverse-reachable`, `own-source-changed`, `new-since-baseline` |
| `changes` | indices into the top-level `changes` array |
| `pathClass` | `compiled` or `widened` |
| `paths` | hop-by-hop, only under `--paths`. Every hop names a method |

The rules are not interchangeable, and only the first is an analysis result. **`changes` is
empty, and `pathClass` absent, exactly when `reverse-reachable` is absent** — which is what makes
an empty list a fact rather than a bug. A test selected because its own source changed has
nothing to point at, and saying so is not the same as failing to compute it.

**`pathClass` is the weakest edge provenance on the *strongest* path.** A test reachable by any
fully compiled path reads `compiled` even if a widened path also exists; one reachable only
through widening reads `widened`. That inverts naively taking the worst edge, and it is the
right instrument: `widened` means *"over-selection is plausible here"*.

## `changes`

Keyed on **changes**, not on expanded roots. Whole-assembly widening puts every method of an
assembly into the changed set, so one unattributable file would otherwise expand into ten
thousand entries.

| Field | Notes |
|---|---|
| `index` | what `tests[].changes` refers to |
| `display` | the member, type, assembly or path that changed |
| `tier` | `member` · `whole-type` · `whole-assembly` |
| `reason` | why it was routed that way |
| `testsReached` | **counts only, never names** |

**`testsReached: 0` is the most valuable line in the document.** It makes *"I changed
`OrderService.Submit` and nothing runs"* something you read rather than an absence you have to
notice — and that is either a genuine coverage gap or an under-selection bug. Today both are
invisible everywhere else.

## `summary`

| Field | Notes |
|---|---|
| `selected` | the canonical selection |
| `willRun` | what the rendered filters actually match — **the numerator** |
| `total` | enumerable test methods in in-scope test projects |
| `projectsRunInFull` | projects on an unrecognised framework, contributing to neither side |
| `widenedPairs` | (test, change) pairs whose path class is `widened` |
| `ratio` | `willRun / total` to four places, or **`null`** when nothing was enumerable — never a zero denominator |

The numerator is what **will run**, not what was selected: a dialect-level over-match *is*
over-selection. The denominator is unweighted by duration, which is the more honest instrument
and is unavailable — Reach does not run tests. Both are recorded as limitations rather than
worked around, and the [stop condition](limitations.md#the-stop-condition) says what the numbers
would have to say for this to have been the wrong thing to build.

## Reading a surprising selection

Walk [`example-report.json`](example-report.json). It is one change to
`Contoso.Invoice.Tax(int)` in a five-project solution.

**1. What did Reach think changed?** `changes` has one entry:

```json
{ "index": 0, "display": "Contoso.Invoice.Tax(int)", "tier": "member",
  "reason": "the member changed", "testsReached": 2 }
```

`tier: member` means Reach matched the change to a method and walked from it — the precise case.
Had it said `whole-assembly`, the `reason` would say why it could not.

**2. Was the baseline what you expected?** `envelope.baseline` carries the resolved SHA and how
it was found. This one reads:

```json
{ "sha": "…", "reference": "…", "detectedFrom": "Option", "isHead": true }
```

`detectedFrom: "Option"` means it came from `--base` rather than from a CI variable, and
**`isHead: true` means only uncommitted work could appear as a change** — a `baseline-is-head`
notice says the same thing in prose. That is the single most common reason a report looks
emptier than you expected, and the example carries it on purpose. Here it is correct: the fixture
edits a file and does not commit it.

**3. Why was this test selected?** `entries[2].tests[0]`:

```json
{ "display": "Contoso.Tests.InvoiceTests.Totals_include_tax",
  "rules": ["reverse-reachable"], "changes": [0], "pathClass": "compiled" }
```

`reverse-reachable` from change `0`, over a path with no widened edge in it. That is the
strongest answer Reach gives: the call graph says this test can reach that method through
compiled instructions only. Run it with `--paths` to see every hop.

**4. Why is something running that you did not expect?** Two places to look.

`entries[0]` — the NUnit project — selected one test and will run two:

```json
"counts": { "selected": 1, "willRun": 2, "total": 3 }
```

The `notices` array says why: `nunit-filter-overmatch`, and `dialect-over-selects` names the
gap. The filter renders as `FullyQualifiedName~…MyTest`, which also matches `MyTest2`.

`entries[1]` — the unrecognised-framework project — runs in full with `total: "unknown"`, and
`whole-project-fallback` says so.

**5. What could Reach not see?** Every notice of kind `blind-spot` is a gap that may have cost
you a test, and every one of them has an entry in the [limitations register](limitations.md) —
a test in Reach's own suite fails the build if it does not.

## The notice-code index

Every code Reach can emit. Codes that are *accepted holes* carry one line and a link; the
register holds the explanation, the direction of failure and the upgrade path. Codes that are
not holes are documented in full here.

### `blind-spot` — may under-select

| Code | |
|---|---|
| `ignored-untracked-assembly` | compiled source that is untracked **and** git-ignored. [Register](limitations.md#ignored-and-untracked-compiled-files) |
| `unmapped-file-no-project` | a changed file matched no rule and sits inside no project. [Register](limitations.md#a-changed-file-matching-no-rule-and-no-project) |
| `parse-failed` | a changed file did not parse cleanly, so its project widened. [Register](limitations.md#c-newer-than-reachs-parser-misparsed-silently) |
| `calli-unresolved` | a function pointer invoked with no visible `ldftn`. [Register](limitations.md#function-pointers-obtained-outside-analysed-il) |
| `unresolved-first-party-member` | a call site names a member of a first-party assembly that is not in it. [Register](limitations.md#a-first-party-member-a-call-site-names-but-the-assembly-does-not-contain) |

### `widening` — may over-select

| Code | |
|---|---|
| `signature-ambiguous` | a signature matched more than one method, so every candidate got an edge. [Register](limitations.md#ambiguous-signatures-edge-to-every-candidate) |
| `whole-project-fallback` | a test project's framework is unrecognised, so it runs in full. [Register](limitations.md#whole-project-selection-on-an-unrecognised-test-framework) |
| `nunit-filter-overmatch` | NUnit renders with containment, so the filter can run more than was selected. [Register](limitations.md#the-nunit--filter-over-match) |
| `dialect-over-selects` | names the extras when the count is non-zero. `data.projects` |
| `runsettings-filter-conflict` | the supplied runsettings already filters, so the project runs in full. [Register](limitations.md#a-callers-testcasefilter-downgrades-widening-to-whole-project-selection) |
| `framework-selector-underivable` | a multi-targeted project's moniker cannot be recovered, so it runs in full. [Register](limitations.md#a-platform-suffixed-target-framework-downgrades-its-project-to-whole-project-selection) |
| `recompilation-widening` | a removal or a changed compile-time constant widened every transitive referencer. [Register](limitations.md#recompilation-widenings-blast-radius) |

### `scope` — where a change landed

| Code | Meaning | `data` |
|---|---|---|
| `project-outside-every-solution` | Reach was pointed at a project no solution above it lists, so there is no expected project list to check the output against | `projects` |
| `untracked-source-files` | C# files git has never seen entered the changed set: intentional codegen, or a forgotten `git add` | `paths` |
| `no-changed-member-reached-a-test` | there were changes, they joined, and no test could reach any of them | — |
| `all-changes-outside-analysis-scope` | every change was outside the union of the test projects' closures | — |
| `changes-were-formatting-only` | paths changed, but nothing inside them did | — |

The last three appear in `reasons` rather than in `notices`.

### `environment` — facts about the run

| Code | Meaning | `data` |
|---|---|---|
| `baseline-resolved` | how the baseline was arrived at, and what it resolved to | `sha`, `reference`, `origin`, `variable` |
| `baseline-is-head` | the baseline is `HEAD`, so only uncommitted work can appear as a change | `sha` |
| `baseline-unresolvable` | no baseline could be found; the message prints the line to add | `problem` |
| `langversion-above-ceiling` | a project declares a language version above what Reach's parser understands. Reported, **not** widened | — |
| `selection-delivered-out-of-band` | the selection exceeded the command-line ceiling, so it went through a response file or several invocations | — |
| `test-runner-not-configured` | a project needs the Microsoft.Testing.Platform runner setting and `global.json` does not have it, so every command Reach emitted is unrunnable. The rendering is correct; the repository is not | `projects` |

### Reserved `data` keys

`projects`, `assemblies`, `paths` and `members` mean the same thing wherever they appear, which
is what lets you build *"everything affecting this project"* without knowing every code. Every
other key is free-form per code, and typing each one here would make every new code a breaking
change.

**Notices cannot be suppressed.** A suppression switch would un-surface exactly the gaps the
correctness bargain depends on surfacing. If suppression ever ships, `blind-spot` must be
non-suppressible.
