# The report writer, exit codes and the human summary

Status: ready-for-agent
Depends on: 11
Spec: [§13](../../walking-skeleton/spec.md#13-the-report), [§14.1](../../walking-skeleton/spec.md#141-two-closed-sets)–[§14.2](../../walking-skeleton/spec.md#142-exit-codes)

## Goal

`report.json` — the product surface everything else is judged by — plus the human summary and the
exit codes that make PRD §8's invariant operational.

## Scope

**Three of this document's decisions exist to make a dangerous state *unrepresentable* rather
than merely documented.** Keep them that way.

**One report per run**, with entries keyed on **(test project, target framework)** uniformly,
**even for a single-targeted project**, so there is no special case in the shape. The TFM is part
of method identity, so two frameworks of one project can legitimately select differently
(`#if NET9_0`); collapsing them would force a union — safe, but needless over-selection — and it
destroys the answer to *"why did `net8.0` run this?"*. A pipeline that prefers one invocation can
coalesce; it cannot un-union.

Each entry carries `project`, `targetFramework`, `framework`, `packageVersion`, `runnerHost`,
`dialect` (all four are needed, because the package version changed xUnit's filter surface),
`mode`, `delivery`, `counts`, `tests`, and an **`invocations` array**.

**`invocations` is always an array** — many when chunked, one for `run-all`, **zero for `skip`**.
That is the load-bearing part: a consumer iterating blindly does nothing for an empty selection,
so "emit no command at all" becomes the only **representable** answer rather than a rule to
remember. It is the only host- and SDK-version-independent answer, since `--ignore-exit-code 8`
stopped working on the .NET 11 SDK. **Argv arrays, never shell strings**: there are already three
escaping layers — filter grammar, MSBuild, XML — and a shell would be a fourth nobody should own.

**`total` must be able to say `unknown`.** A project on an unrecognised framework cannot be
enumerated; reporting `0` would silently corrupt the measurement. **Every test project appears
even with an empty selection**, so a pipeline can skip it deliberately rather than by absence.

**Two closed sets.** Run-level `outcome`: `selected` · `nothing-selected` · `no-changes` ·
`failed`. Entry-level `mode`: `filtered` · `run-all` · `skip`. **No run-level "everything was
selected" outcome** — derivable from the modes.

**On `no-changes`, the full project list is still emitted**, every entry `skip` with zero
invocations, so a consumer's loop behaves identically across all four outcomes. Omitting entries
would make it a special case, and the consumer that forgets runs the full suite on an empty diff.

**`reasons` is a list of notice codes, not prose**, required non-empty when the outcome is
`nothing-selected`. The consequence matters more than the deduplication: every cause of emptiness
must be a documented, register-linked code — `no-changed-member-reached-a-test`,
`all-changes-outside-analysis-scope`, `changes-were-formatting-only` — so nobody adds one as an
ad-hoc string, and the emptiness taxonomy becomes enumerable and testable.

**Scope** enumerates test projects always, and in-scope assemblies as a flat list of identities
(name plus TFM). **Never closure edges** — those are the project graph and belong nowhere near a
report.

**Changes outside the analysis scope are reported with a category**, never selected on: *outside
any project*, or *inside a project no test project's closure reaches*. The second reads as
*"nothing in this repository can test this code"* — the honest answer for an untested worker or
console project, and better than a warning.

**Naming a method: two fields.** `display` — fully-qualified type, method name, generic arity,
fully-qualified parameter types, assembly and TFM — and `id`, stable within a run and comparable
across runs of the same tool version. **Roots and path hops render as kernel methods**, never
`<Submit>b__0_1` or `MoveNext`; where a compiler-generated member is the interesting fact it
appears as a detail *on* the hop, not as its name.

**The envelope**: schema version, tool version, the baseline **as resolved to a SHA** plus how it
was detected, the HEAD SHA, change sources included, build mode, the forwarded build argv, the
correspondence verdict, and **Reach's own phase timings** plus the build duration when Reach ran
the build. No test durations. Timings and start time are **segregated into this one
clearly-marked object**, so excluding one key makes two reports comparable.

**Determinism.** Byte-deterministic for a given input: projects sorted by path, tests by
fully-qualified name, changes by display form, notices by code then locator. **No size ceiling** —
report size correlates with the selection being surprising, so a ceiling would strip the detail
exactly when it is wanted. If a root cap ever fires, it **keeps at least one change of every path
class** before filling by sort order and always reports the true total: an arbitrary prefix could
hide that a test was reached *only* through widening, which is precisely the fact that decides
whether to believe the selection.

**`schemaVersion` is a single integer, and the consumer's obligation is part of the contract:
tolerate unknown fields, unknown enum members and unknown notice codes.** That tolerance is what
makes new codes, kinds and outcomes additive; without it every code the register gains would be
breaking. Breaking means removing a field, repurposing one, or changing its type. `toolVersion`
stays separate — a bug fix is not a schema change. **M1 emits version `1` and cannot emit any
other.**

**A report is written even when the outcome is `failed`**, because a tool that writes nothing when
it fails is one you debug by re-running it with more flags, on CI, which is the worst place to
need a second run. The selection-bearing sections are therefore **optional rather than required**
in the schema. **The one exception: a usage error writes no report**, because the arguments were
never valid enough to establish where to write one.

**Exit codes**: `0` success including an empty selection · `1` usage error · `2` build failed ·
`3` assembly missing or discovery failed · `4` baseline unresolvable · `5` correspondence failed ·
`70` internal error. **An empty selection exits 0**, which makes the invariant one line of
pipeline guidance: *if Reach exits non-zero, run the whole suite or stop the build.* Codes are
needed only for PRD §8's error rows — an unparseable project graph **widens** (success, with
`run-all` entries) rather than failing.

**The human summary is explicitly not a contract**, and the documentation says so, so nobody
builds a `grep` pipeline against it. It carries the outcome, per-entry mode and counts, notice
counts by kind **with every `blind-spot` notice printed in full**, the resolved baseline SHA and
how it was detected, and where the report and side-car files went. **Not the selected test list.**

Three things it must get right, because a normal run **is** the preview — there is no dry-run
verb, since `select` does not run tests and a second verb would create two code paths obliged to
agree:

- **The resolved baseline SHA and its detection source lead every run.** An audit line nobody
  reads does not catch a baseline that resolved to `HEAD`.
- **`no-changes` and `nothing-selected` get visibly different verdicts**, not two shades of
  "0 tests". They exit the same and mean opposite things.
- **Over-selection is stated as a percentage of the suite** (ticket 18 defines it), because that
  is the number that decides adoption.

When the outcome is `nothing-selected`, **the reason codes lead the output** — it is the result
most likely to be disbelieved.

## Acceptance criteria

- **Determinism**: the same input twice produces byte-identical JSON once the envelope's timings
  are excluded. This is the assertion that makes exact expected selections cheap everywhere else,
  so it comes first.
- A single-targeted project still produces a (project, TFM)-keyed entry.
- `outcome: no-changes` emits every test project with `mode: skip` and `invocations: []`.
- `nothing-selected` without a non-empty `reasons` array is a test failure.
- A `failed` run writes a report whose envelope is complete; a usage error writes none.
- `total: unknown` round-trips and is distinguishable from `0`.
- Sort order is asserted for each array, including notices sorted by code then locator.
- Every outcome and every exit code is reachable from a test.
- The summary prints the baseline SHA and its detection source on every run, and gives
  `no-changes` and `nothing-selected` visibly different verdicts.
- A golden summary test for the `nothing-selected` case, asserting reason codes lead.

## Out of scope

Filling `invocations` — ticket 13. The notice catalogue and the measurement fields — ticket 18;
this ticket writes the shape and the sort, not the codes.
