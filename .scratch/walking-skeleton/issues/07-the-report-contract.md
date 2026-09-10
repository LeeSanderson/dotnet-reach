# The report contract

Type: grilling
Status: resolved
Blocked by: (none — 01 resolved)

## Question

The JSON report is the canonical, framework-agnostic form of a selection — every dialect
renders from it, and PRD §4.3 is blunt about why it matters: "without it nobody can debug
a surprising result, and a tool nobody can debug gets switched off." PRD §12 makes
debuggability a success criterion. So the schema is a product surface, not an
implementation detail.

**What it must contain.** PRD §4.3 lists the selected tests, the total test count, the
changed methods that drove each selection, frameworks detected without a model, and
projects that fell back to coarse selection. Charting added more: per-test-project
grouping so a pipeline can skip projects entirely; the tier each selection came from; and
edge provenance along the path, so a selection can be explained hop by hop (ADR-0004).

**Open questions:**

- Is the full reverse path recorded for every selected test, or only the changed method
  that reached it? Full paths are what make surprises explicable and could be very large.
- How are *unselected* tests represented — by omission, by count, or explicitly? PRD §4.3
  says the report covers "what was not" selected.
- What does the report say when the answer is "nothing was selected"? That is the result
  most likely to be disbelieved.
- Does it carry build and test durations? Deferred here from charting: PRD §11's
  build-to-test ratio measurement is out of scope as a milestone, but if the report
  carries the numbers, the ratio comes free on first real use.
- Schema versioning, so consumers can depend on it while it evolves.
- Where output goes: stdout, a file, a file per project; and how the rendered filters are
  delivered alongside the JSON.
- **Exit codes and the error taxonomy.** PRD §8's table specifies behaviour for a failed
  build, a missing assembly, an unparseable project graph, and a thrown framework model.
  Each needs a distinct, documented exit code so a pipeline can tell "select nothing
  because nothing was affected" from "select nothing because Reach failed" — conflating
  those is how the invariant gets violated in practice.

**Carried in from the dialect research:** `--ignore-exit-code 8` — the usual way to stop a
zero-match run failing the build — **stops working on the .NET 11 SDK**, where zero-match
handling moved to a run-level verdict. Emitting no command at all for an empty selection is
the only version-independent answer, which makes "how is an empty selection delivered" a
contract question rather than a rendering detail.

**Carried in from ticket 16:** Microsoft's own MTP work delivers a selection as a
**test-node UID list** (`--filter-uid` / `TestNodeUidListFilter`) rather than a filter
string, and that path has no command-line length limit — which would dissolve the length
ceiling entirely for MTP hosts. The catch to weigh: UIDs come from the platform's own
discovery, so obtaining them may require a discovery pass Reach does not otherwise run.
Decide whether MTP renders as UIDs or as a filter string, and what that costs.

The dialect research is resolved; this ticket is now unblocked.

## Comments

**From [Change-set edge cases](04-change-set-edge-cases.md):** that ticket decided *which
facts* the report owes and deliberately left the shape here. Five of them:

1. **The analysis scope itself** — which test projects, and which assemblies their closures
   cover. ADR-0002 permits pointing Reach at a single test project, and "your change is
   inert" versus "you asked me to look at one project" are very different news that produce
   identical selections.
2. **Every unattributed changed path, with a category** — *outside any project*, or *inside
   a project no test project's closure reaches*. The second reads as "nothing in this
   repository can test this code", which is the honest answer for an untested worker or
   console project and better than a warning.
3. **An empty change set as its own named outcome**, distinct from an empty selection. A
   baseline resolving to `HEAD` produces an empty diff, an empty selection, a green pipeline
   and no tests run, while every other guard passes happily.
4. **Untracked `.cs` files that entered the change set** — either intentional codegen or a
   forgotten `git add`, and both bear on trusting a surprising selection.
5. **First-party PDB documents that are untracked and git-ignored** — compiled source Reach
   cannot observe changes to. Reported, never selected on, and suppressed under `obj/` and
   `bin/` path segments.

Standing rule from that ticket: **an empty selection is never emitted without an
accompanying reason list.** It reinforces the position already carried in above — emitting
no command at all for an empty selection — since an empty `--filter` string runs everything.

## Answer

The report is a product surface with a versioning promise, and three of its decisions exist
to make a dangerous state *unrepresentable* rather than merely documented.

### The document

**One report per run**, grouped by **(test project, target framework)** — uniformly, even
for a single-targeted project, so there is no special case in the shape. The map already
decided every target framework is analysed and that the TFM is part of method identity, so
two TFMs of one project can legitimately select differently (`#if NET9_0`). Collapsing them
into one entry would force a union: safe, but needless over-selection, and it destroys the
answer to "why did `net8.0` run this?". A pipeline that prefers one invocation can coalesce;
it cannot un-union.

Each entry carries a **mode** — `filtered`, `run-all`, `skip` — plus the framework,
**package version**, runner host and dialect it was rendered for (all four are needed,
because 4.0.0 changed xUnit's filter surface), its counts, its selected tests, and an
**`invocations` array of argv vectors**.

`invocations` is always an array: many when chunked, one for `run-all`, and **zero for
`skip`**. That is the load-bearing part. A consumer iterating invocations blindly does
nothing for an empty selection, so "emit no command at all" — the only host- and
SDK-version-independent answer to an empty selection, since `--ignore-exit-code 8` died on
the .NET 11 SDK — becomes the only *representable* answer rather than a rule to remember.
Argv arrays, never shell strings: there are already three escaping layers (filter grammar,
MSBuild, XML) and a shell would be a fourth nobody should own.

**Human summary to stdout, warnings to stderr, JSON to a file** — with the JSON able to go
to stdout for containerised pipelines, in which case the summary moves to stderr. The
summary is explicitly **not a contract**; say so in the docs, so nobody builds a `grep`
pipeline against it. It carries the outcome, per-entry mode and counts, notice counts by
kind **with every `blind-spot` notice printed in full**, the resolved baseline SHA, and
where the report and side-car files went. Not the selected test list. When the outcome is
`nothing-selected`, the reason codes lead the output — that is the result most likely to be
disbelieved.

### Explanation: keyed on changes, not expanded roots

The ticket assumed "roots per selected test". Doing the arithmetic moves it: **whole-assembly
widening puts every method of an assembly into the changed set**, so one unattributable file
expands into ten thousand roots, and every test in that assembly's dependents carries all of
them.

So the change side of the report is keyed on **changes** — a changed member, or a changed
file or artifact routed at a coarser tier — with the expansion left as an implementation
detail of the walk. A whole-assembly widening contributes *one* root: "assembly
`Acme.Orders` widened, because `Acme.Orders.csproj` could not be attributed to a member".
Bounded, and it is the sentence a developer actually needs. It also means the root cap
almost never fires.

Which settles something the ticket left loose: **the tier lives on the change, not on the
pair.** The ladder routes per change and unions, so the tier is a property of routing; a
(test, change) pair inherits it by reference.

**Per selected test:**

- A **`rules` array** from `reverse-reachable`, `own-source-changed`, `new-since-baseline` —
  PRD §4.2 selects by four rules and everything designed here covered only the first. A test
  new since the baseline has no root and no path class, which under the old shape produced an
  empty roots array indistinguishable from a bug. Now an empty roots list is valid *exactly
  when* `reverse-reachable` is absent. The unanalysable-project rule is deliberately absent
  from this set: `mode: run-all` already says it.
- For reverse-reachable tests, the **changes** that reached it, each with a **path class**:
  *the weakest edge on the strongest path*. So a test reachable by any fully-compiled path
  reads `compiled`, and one reachable only through widened dispatch reads `widened`. That
  inverts naively taking the worst edge, and it is the right instrument — `widened` means
  "over-selection is plausible here", which is what ticket 17 needs and the only class
  narrowing may ever safely touch (ADR-0004).
- Hop-by-hop paths stay **opt-in**. They are the one genuinely unbounded thing in the
  document.

**Forward-indexed alongside it: every change, with its tier and the count of tests it
reached.** This is arguably the most valuable field in the report — it makes "I changed
`OrderService.Submit` and nothing runs" a line you read rather than an absence you have to
notice, which is either a genuine coverage gap (useful) or an under-selection bug (critical),
and today both are invisible. Affordable because of the change-keying above: the list is
diff-sized, not graph-sized. It carries **counts only, never test names** — the pairs live
once, on the test side — and a notice points at it when any change reached zero tests.

**Unselected tests are counted, not enumerated** (enumeration opt-in), and every test project
appears even with an empty selection so a pipeline can skip it deliberately rather than by
absence. `total` **must be able to say `unknown`**: a project on an unrecognised framework
cannot be enumerated, and reporting `0` would silently corrupt ticket 17's ratio.

### The canonical selection and the rendered filter deliberately disagree

The NUnit decision renders `FullyQualifiedName~Ns.C.MyTest`, which also matches `MyTest2`.
So **what actually runs is a strict superset of what the report says was selected**, and that
gap was invisible.

The report therefore carries **both**: the canonical count, and the rendered filter's true
match set, obtained by applying the dialect's own matching semantics back over the enumerated
test list — cheap, since Reach already enumerates every test method to produce the total, and
for M1 only NUnit's `~` over-matches, so it is one substring pass. A `dialect-over-selects`
notice names the extras when non-zero.

Not pedantry: **ticket 17 would otherwise measure the wrong number.** Over-selection is
"tests that ran but did not need to", and a deliberate dialect-level over-match is exactly
that; reporting only the canonical count understates Reach's real over-selection by whatever
the `~` rendering adds.

### Trust: one notice channel

**A single global `notices` array**, with locators — never notices on entries as well. An
unmodelled framework affecting three projects would otherwise be duplicated three times or
split across two channels, and a consumer would have to read both to be confident it had seen
everything. The `projects` locator gives the per-project view for free.

Each notice: a **stable kebab-case `code`, never renamed** (`unmodelled-framework`,
`untracked-source-files`, `callback-from-outside-scope`) — not `REACH1042`. The numeric
convention exists because compilers emit thousands of diagnostics needing terse bulk
suppression; Reach will have dozens, they are read rather than suppressed, and a slug carries
most of the explanation to the person staring at a surprising selection. It also gives the
limitations register a natural anchor per hole instead of a lookup table.

**One `kind` field and no severity axis**: `blind-spot` (may under-select — every ADR-0008
hole), `widening` (may over-select), `scope`, `environment`. A `warning`/`info` axis alongside
this could only ever disagree with it, and a pipeline that wants to gate gates on
`blind-spot`.

**`data` is free-form per code**, documented in the register, with two constraints: the human
`message` must be complete on its own, so a consumer ignoring `data` loses structure but never
meaning; and the names `projects`, `assemblies`, `paths`, `members` are **reserved and used
consistently** wherever they apply, which is what lets a consumer build "everything affecting
this project" without knowing every code. Typing each code in the schema would make every new
code a breaking change, which fights the additive rule below.

**`reasons` is a list of notice codes**, not prose — a pointer into `notices`, required
non-empty when the outcome is `nothing-selected`. The deduplication matters less than the
consequence: every cause of emptiness must now be a documented, register-linked code
(`no-changed-member-reached-a-test`, `all-changes-outside-analysis-scope`,
`changes-were-formatting-only`), so nobody adds one as an ad-hoc string. The emptiness
taxonomy becomes enumerable and testable.

**Every `blind-spot` code must have a limitations-register entry, with parity asserted by
Reach's own test suite.** That is ADR-0008's "named and surfaced" clause made mechanical
rather than a promise. **No suppression in M1** — a suppression switch is a mechanism for
un-surfacing exactly the holes the bargain depends on surfacing, and there is no evidence yet
about which codes are noisy. If it ever lands, `blind-spot` must be non-suppressible.

### Outcomes and exit codes

Two levels, both closed sets. **Run-level `outcome`**: `selected`, `nothing-selected`,
`no-changes`, `failed`. **Entry-level `mode`**: `filtered`, `run-all`, `skip`. No run-level
"everything was selected" outcome — it is derivable from the modes, and a second way to say it
is a second thing to keep consistent.

On `no-changes`, **the full project list is still emitted**, every entry `skip` with zero
invocations, so a consumer's loop behaves identically across all four outcomes. Omitting
entries would make it a special case, and the consumer that forgets runs the full suite on an
empty diff.

**Exit codes carry the outcome**, from a small documented set, with one hard rule: **an empty
selection exits 0.** Non-zero means "do not trust my answer", which makes PRD §8's invariant
operational as a single line of pipeline guidance: *if Reach exits non-zero, run the whole
suite or stop the build.* Codes are needed only for §8's error rows, since an unparseable
project graph *widens* (success, with `run-all` entries) rather than failing. The shape:
`0` success · `1` usage error · `2` build failed · `3` assembly missing or output incomplete ·
`4` baseline unresolvable, including a shallow clone · `5` source–binary correspondence failed
(ADR-0003) · `70` internal error.

**A report is written even when the outcome is `failed`.** At the point of failure the
envelope holds exactly what is needed — resolved baseline, build mode, correspondence verdict,
tool version — and a tool that writes nothing when it fails is one you debug by re-running it
with more flags, on CI, which is the worst place to need a second run. The schema therefore
makes the selection-bearing sections **optional rather than required**, which is the concrete
reason the tolerance rule below matters. The exception: a **usage error writes no report**,
because the arguments were never valid enough to establish where to write one.

### Delivery: each host's private file channel, and one degraded host

The length ceiling is not an edge case. A `FullyQualifiedName=…` clause runs 60–90 characters,
so `cmd`'s 8,191 ceiling arrives at roughly **100 selected test methods** and
`CreateProcessW`'s 32,767 at roughly **400**. The escape hatch is the normal path.

Research 01's table splits cleanly once you ask *who owns the channel*: MTP takes `@file.rsp`,
xUnit's native CLI takes `@@ file`, nunit-console takes `--testlist` — all Reach's own files,
passed as arguments, unable to collide with anything the consumer configured. **VSTest under
`dotnet test` is the one host with no private channel** (closed as not planned).

For that host, **chunk into multiple invocations** and accept the cost. Do *not* default to
`.runsettings` `<TestCaseFilter>` despite its unlimited length: a pre-existing
`<TestCaseFilter>` is AND-ed with Reach's, which is an undetectable under-selection, and only
one `--settings` file can be passed at all, so writing one would silently drop the consumer's
other configuration. Reach cannot see the consumer's runsettings — the same blindness that
forced the NUnit `~` decision — so it cannot make this safe by detection.

Runsettings survives only as an **explicit opt-in**: if the caller hands Reach their
runsettings path, Reach may merge and emit a combined file, and if that file already contains
a `<TestCaseFilter>`, Reach **refuses to filter that project and downgrades it to `run-all`**,
per the widen-when-uncertain rule. Every chunk and every downgrade emits a notice.

Recorded as [ADR-0009](../../../docs/adr/0009-per-host-private-filter-channels-over-runsettings.md),
because a future reader will find the unlimited runsettings hatch and need to know why it was
rejected. Rejected alternative: invoking `vstest.console.exe` directly, which *does* accept
`@file` — locating it means finding a Visual Studio or test-platform installation, a discovery
problem larger than the one it solves.

**Side-car files** live in a single Reach-owned directory the caller names, defaulting beside
the report, with deterministic names from project + TFM + chunk index so a re-run overwrites
instead of accumulating, and **absolute paths in the argv** so a pipeline that changes
directory between steps still works. **Never** a path MSBuild or Visual Studio reads by
convention — no `Directory.Build.props`, no project-root `.runsettings`. **Reach does not
clean up**: the runner reads these files in a *later pipeline step*, so deleting them on exit
would break the contract Reach just published; the caller owns the directory's lifecycle. Not
the system temp directory — agents clear it between steps and it is the least inspectable
place to look when a filter misbehaves.

### Naming a method, the envelope, and the schema

**Two fields per method: a `display` string this ticket fixes, and an `id` whose spelling
ticket 09 owns.** The display form is fully-qualified type, method name, generic arity,
fully-qualified parameter types, assembly and TFM — derivable from *either* join candidate, so
the report does not presuppose whether the join lands on signature keys or PDB line spans. The
contract asks only that `id` be stable within a run and comparable across runs of the same
tool version.

**Roots and path hops render as kernel methods**, never `<Submit>b__0_1` or `MoveNext`.
`CONTEXT.md` already says a change inside a state machine or lambda *is* a change to the
kernel method, and a report naming compiler-generated members reads as a bug to everyone who
sees it. Where the compiler-generated member is the interesting fact — an ADR-0006 containment
edge reconnecting an `async` body — it appears as a detail *on* the hop, not as its name.

**Scope**: enumerate test projects always; enumerate in-scope assemblies as a flat list of
identities (name plus TFM); **never** closure edges, which are the project graph and belong
nowhere near a report. Three hundred assembly identities is a few KB, cheap beside the
selection.

**Envelope**: schema version, tool version, the baseline **as resolved to a SHA** (not
`origin/main`), the HEAD SHA, which change sources were included, the build mode, and the
ADR-0003 correspondence verdict. The resolved baseline earns its place alone: ticket 04's
empty-change-set trap *is* a baseline that quietly resolved to `HEAD`, and an audit trail that
cannot say what it audited will not catch it.

**Durations**: Reach's own phase timings, plus the build duration when Reach ran the build. No
test durations — Reach does not run tests. The timings defend PRD §9.4's budget for free and
put half the build-to-test ratio in the report without reopening the M0 measurement.

**`schemaVersion` is a single integer**, with the consumer's obligation written into the
contract: **tolerate unknown fields, unknown enum members and unknown notice codes.** That
tolerance is what makes new codes, kinds and outcomes additive; without it every code the
register gains would be breaking, which would strangle the notice design. Breaking means
removing a field, repurposing one, or changing its type. `toolVersion` stays separate — a bug
fix is not a schema change. M1 emits version `1` and cannot emit any other.

**The report is byte-deterministic for a given input**, every array sorted by a documented key
(projects by path, tests by fully-qualified name, changes by display form, notices by code then
locator), with the timings and start time **segregated into one clearly-marked envelope
object** so that excluding one key makes two reports comparable. Better to name the exception
than pretend the document is stable. **No size ceiling**: report size correlates with the
selection being surprising, so a ceiling would strip the detail exactly when it is wanted.

If the root cap ever fires, it **keeps at least one change of every path class present** before
filling by sort order, and always reports the true total — an arbitrary prefix could hide that
a test was reached *only* through widening, which is precisely the fact that decides whether to
believe the selection.

### MTP test-node UIDs: out of scope

M1 renders a filter string. The UID path (`--filter-uid`, `TestNodeUidListFilter`) would
dissolve the length ceiling entirely for MTP hosts, but UIDs come from a discovery pass Reach
does not run, and the in-process provider path is `[Experimental]` and needs a code change in
the consumer's test project — an infrastructure ask of exactly the kind PRD §9.1 rejects.
Ruled out of scope on the map with the revisit trigger recorded.

### Escalated elsewhere

- **[What counts as a changed member](18-what-counts-as-a-changed-member.md)**: an
  attribute-only change must count. Adding `[Fact]` to an existing method creates a new test
  **without changing its body**, so a body-hash change set misses it — an under-selection the
  correctness rule forbids, and the reason `new-since-baseline` is kept as a rule distinct from
  `own-source-changed` rather than derived from it.
- **[The limitations register](19-the-limitations-register.md)**: the code-parity requirement
  and the non-suppressible rule.
- **[Fixture catalogue](11-fixture-catalogue.md)**: four fixtures.
- **[Defining the over-selection measurement](17-defining-the-over-selection-measurement.md)**:
  the dialect over-match count as an input.
- **[CLI surface](08-cli-surface.md)**: now unblocked, with the flags this contract implies.
- **[PRD amendments](06-prd-amendments.md)**: two items.
