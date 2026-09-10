# CLI surface

Type: grilling
Status: resolved
Blocked by: (none — 03, 07 resolved), and informed by 14

## Question

What `dotnet reach` looks like from the outside. PRD §12 makes adoption cost a success
criterion — "a competent engineer can add Reach to an unfamiliar pipeline in under an
hour, using only documentation" — so every required argument is a tax on that.

**Commands.** PRD's appendix suggests `dotnet reach select` and `dotnet reach watch`.
`watch` is M3. Is `select` the only M1 command, and is it the default when no verb is
given?

**Inputs already decided, needing a surface:** the target (a `.sln`, `.slnx` or `.csproj`);
the baseline (merge-base by default, an explicit ref available); which change sources
count (committed, staged, unstaged, untracked); build configuration; output root, since
`UseArtifactsOutput` and `OutputPath` overrides cannot always be detected; `--no-build`.

**Open questions:**

- Which inputs have safe defaults and which must be supplied? Every default is a place
  Reach can be confidently wrong.
- What happens with no arguments at all, in a repository root containing one solution?
  That path is the adoption criterion.
- Ambiguity: several solution files, or a project outside every solution.
- Is there a mode that reports what Reach *would* do without emitting a filter — useful
  for the first run on an unfamiliar repository, where the honest answer may be "this
  codebase over-selects so heavily that Reach is not worth adopting".
- Verbosity, diagnostics, and how a surprising selection is investigated from the command
  line rather than by reading JSON.

Blocked on packaging and CLI-library research, and on the report contract, since where
output goes is an argument.

## Comments

**From [Change-set edge cases](04-change-set-edge-cases.md):** that ticket established that
an empty selection is a distinguishable *outcome* carrying its reason, and left the encoding
here. Two things to settle:

- **How the outcome is signalled to a pipeline.** A changed set that attributes to nothing —
  a docs-only PR — is a legitimate, valuable answer and must not fail the build, so it is
  not an error. But it is indistinguishable from a misconfiguration unless something
  distinguishes it. A distinct non-error exit code was considered and routed here rather than
  decided there.
- **An empty selection must never render as an empty `--filter` string**, which runs
  everything. Ticket 07 carries the rendering half (emit no command at all); this ticket owns
  whatever the CLI *says* and returns when that happens.

That ticket also ruled out a `--no-untracked` flag: untracked files are always included, on
the grounds that the flag's only possible effect is under-selection and the pathological case
belongs in the consumer's `.gitignore`. One fewer argument in the option budget.

**From [The report contract](07-the-report-contract.md):** resolved, so this ticket is
unblocked. It settles the two questions routed here and spends some of the option budget.

The empty-selection questions are answered on the contract side: the outcome is a closed set
(`selected` / `nothing-selected` / `no-changes` / `failed`), an empty selection **exits 0**,
and a project with nothing selected gets **zero invocations** rather than an empty filter. So
this ticket no longer has to invent a signalling scheme — it owns what the CLI *prints*, and
the one line of pipeline guidance that falls out of the exit-code decision: **if Reach exits
non-zero, run the whole suite or stop the build.** Note that `no-changes` and
`nothing-selected` are separate outcomes but both exit 0; whether the CLI distinguishes them
loudly enough to catch a baseline that resolved to `HEAD` is a real question for the summary's
design.

Exit codes are fixed as `0` success · `1` usage error · `2` build failed · `3` assembly missing
· `4` baseline unresolvable · `5` correspondence failed · `70` internal error. Only `1` is
this ticket's to shape, since a usage error is the one case that writes **no report**.

**Arguments the contract implies**, all needing a surface and a default:

- Where the JSON goes, with a `-` convention for stdout (which moves the human summary to
  stderr).
- The side-car output directory, for the response files and testlists that carry long
  selections. Must default somewhere git-ignorable and never anywhere MSBuild reads by
  convention (ADR-0009).
- Opt-in hop-by-hop paths in the report — the one unbounded part of the document.
- Opt-in enumeration of *unselected* tests.
- An explicit "here is my runsettings, merge into it" input. This is the only way Reach may
  write a runsettings at all (ADR-0009), and it downgrades the project to whole-project
  selection if that file already contains a `<TestCaseFilter>`.

Two things the contract deliberately did **not** create: no notice-suppression flag in M1, and
no schema-version selection flag. Both stay off the option budget.

Still open above and now sharper: **"a mode that reports what Reach would do without emitting a
filter"** — the report already contains everything such a mode would print, including the
forward list of changes that reached no tests and the over-selection counts. So the question
narrows to whether that mode is a distinct verb or simply the human summary of a normal run.
And **"how a surprising selection is investigated from the command line"** now has an obvious
shape the contract stopped short of deciding: an explain-one-test mode is where hop-by-hop
paths belong, rather than a flag that inflates every report.

## Answer

The surface is one verb, one positional, and eleven options. Two decisions in it are
environmental facts rather than design taste, and both are recorded as ADRs because the code
alone would not explain them.

### The shape

```
dotnet reach select [<SOLUTION|PROJECT>] [options] [-- <dotnet build args>]
```

| Option | Default | Owner |
|---|---|---|
| `<SOLUTION\|PROJECT>` positional | discovered in the working directory | [ADR-0011](../../../docs/adr/0011-a-solution-wins-over-a-project-in-target-discovery.md) |
| `--base <ref>` | auto-detected | [ADR-0010](../../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md) |
| `-c\|--configuration <name>` | the SDK's own default | this ticket |
| `--no-build` | off | charting |
| `--report <path\|->` | `.reach/report.json` | this ticket |
| `--report-dir <dir>` | `.reach` | this ticket |
| `--paths` | off | ticket 07 |
| `--list-unselected` | off | ticket 07 |
| `--runsettings <path>` | unset | [ADR-0009](../../../docs/adr/0009-per-host-private-filter-channels-over-runsettings.md) |
| `-v\|--verbosity <level>` | `normal` | this ticket |
| `--no-color` | auto-detected | this ticket |
| layout hints | — | [ticket 14](14-assembly-discovery-under-ambiguous-output-layouts.md) |

### `select` is required, and bare `dotnet reach` exits 1

The verb is mandatory. Making it the implicit default was rejected on one ground: **the
default mode invokes `dotnet build`**, so a newcomer typing `dotnet reach` to find out what
the tool is would get a multi-minute build of their solution and files written into their
repository. That is the one invocation a newcomer is guaranteed to try. Documentation leads
with `dotnet reach select` from day one, so nothing in it changes when `watch` (M3) arrives.

The adoption criterion is read as `dotnet reach select` needing **no further arguments** — not
as the verb being omissible.

Exit codes matter here because of the contract's invariant, *non-zero means run the whole
suite*. A bare invocation writes no report, so exiting 0 would hand a mis-wired pipeline
"success" plus a missing report, and a consumer looping over `invocations` would run nothing.
Hence: **`--help` asked for explicitly exits 0; being given nothing prints help and exits 1**,
the usage-error code, which is the one case the contract says writes no report.

### Discovery widens where MSBuild errors

Current directory only, never recursive — recursion is where "confidently wrong" lives, since
a monorepo with six solutions would get a coin flip and the wrong solution silently produces a
wrong analysis scope. One solution wins over any number of projects; several solutions, or no
solution and several projects, exit 1 naming what was found; `.slnf` is rejected.

MSBuild's actual rule compares base names and errors when they differ, and Reach deliberately
does not copy it — [ADR-0011](../../../docs/adr/0011-a-solution-wins-over-a-project-in-target-discovery.md),
which also carries the boundary this establishes: **mirroring `dotnet build` governs option
spellings, not resolution semantics.** Where a resolution rule could narrow scope, the
correctness rule outranks the convention.

Two cases the ticket did not list:

- **A `.csproj` declaring no tests.** ADR-0002 makes scope the union of test-project closures,
  so that union is empty. This is a mis-invocation, not `nothing-selected`; reporting "no tests
  affected" would be a lie a pipeline believes. Exit 1, naming the project.
- **A `.csproj` outside every solution.** Legal under ADR-0002 — the solution only ever supplied
  the expected-project list — but it costs the check that turns a missing assembly into an
  error. Emits a `scope` notice and continues.

### One baseline option, and the fallback that does not exist

`--base <ref>`, always merge-base semantics. The two-option design (`--base` for merge-base,
`--baseline` for an exact commit) collapses, because `merge-base(HEAD, C) == C` for any
ancestor commit — the exact case is already covered for every commit anyone would name.

Detection order, the missing `origin/HEAD` fallback, the source-branch traps and the exit-4
message are all in
[ADR-0010](../../../docs/adr/0010-the-baseline-is-auto-detected-with-no-default-branch-fallback.md).
The headline for this ticket: **Reach does not work under default CI settings**, because
`actions/checkout` fetches one refspec at depth 1 and leaves no `origin/<target>` ref and no
history behind `HEAD`. The fix is one line of YAML, which makes exit 4 the likeliest outcome of
anyone's first run and its message a designed artifact — it detects the provider and prints the
literal line to add.

### `--` forwards to the build, except what Reach owns

Not in the original ticket, and the largest hole in the surface. Default mode invokes
`dotnet build`; real pipelines build with `-p:` properties, `--no-restore`, a chosen verbosity,
sometimes a binlog. Without a passthrough, every non-trivial adopter is pushed onto `--no-build`
permanently, quietly making the documented default the minority path.

So everything after `--` is forwarded verbatim, **except** that Reach scans the forwarded tokens
for layout-affecting switches — `-o`, `--output`, `--artifacts-path`, `-c` — and **refuses with
exit 1**, saying they must be given to Reach directly. A forwarded `-o` would change the output
layout Reach then has to predict, which is ticket 14's problem made invisible because the switch
went through a channel Reach did not read. Reach owns layout inputs; everything else is the
build's business. The envelope records the forwarded argv, so a surprising run stays reproducible.

### A normal run is the preview; there is no dry-run verb

The ticket's "mode that reports what Reach *would* do" does not need to exist. `select` does not
run tests — its only effects are the build and writing files — so a normal run already is the
preview, and the honest verdict the ticket wanted ("this codebase over-selects so heavily that
Reach is not worth adopting") is the summary's counts read out loud. A separate verb would create
two code paths obliged to agree.

That puts the weight on the summary, which fixes three things beyond the contract's list:

- **The resolved baseline SHA and how it was detected lead every run.** Ticket 04's trap is a
  baseline that quietly resolved to `HEAD`; an audit line nobody reads does not catch it.
- **`no-changes` and `nothing-selected` get visibly different verdicts**, not two shades of
  "0 tests". They exit the same and mean opposite things — a docs-only PR versus code changes
  that reached nothing, which is either a coverage gap or an under-selection bug.
- **Over-selection is stated as a percentage of the suite**, because that is the number that
  decides adoption and it is already in the report.

Streams follow the contract: summary to stdout, notices and logs to stderr, and `--report -`
moves the summary to stderr. `-v|--verbosity` mirrors the SDK's `quiet|minimal|normal|detailed|
diagnostic`. Colour is auto-detected, with `--no-color` and `NO_COLOR` honoured.

### Reach owns one directory

`.reach/` under the **working directory** — not beside the solution — holding `report.json` and
every response file and testlist. `--report <path|->` moves the JSON alone; `--report-dir <dir>`
moves the whole directory. Never `--output`, which is reserved for build output.

Reach writes `.reach/.gitignore` containing `*` on first use, so the first adopter to run it
locally does not commit a report. Recorded as a judgement call rather than a precedent-backed
one: the .NET SDK does **not** write a `.gitignore` into its artifacts directory — an exhaustive
grep of dotnet/sdk finds none and a real `UseArtifactsOutput` build produces none — and instead
ships `artifacts/` as a line in the `dotnet new gitignore` template. Reach's directory is covered
by no such template, which is what tips it.

Per the contract, Reach never cleans the directory up: the runner reads those files in a *later*
pipeline step. The documentation owes one line saying its lifecycle is the caller's.

### `--runsettings` is documented by its effect

ADR-0009 makes this the only way Reach may write a runsettings at all, and a file already
carrying a `<TestCaseFilter>` downgrades that project to `run-all` with a notice. The name is a
foot-gun: someone will pass it expecting Reach to *use* their settings for a test run Reach never
performs. The help text states the imperative — "merge Reach's filter into this runsettings file
and write the result" — rather than naming the noun.

### Recorded without asking

- **One target per run.** PRD §2 fixes the unit of analysis as a single solution.
- **The chunking ceiling is detected per platform, never a flag.** No adopter can reason about an
  8,191-character limit, so a wrong value is a bug rather than a knob.
- **No option may narrow scope** — no test-project include/exclude, no target-framework
  restriction — refused for the same reason ticket 04 refused `--no-untracked`.
- **`--artifacts-path` is hyphenated and has no short form** on any `dotnet` command, so neither
  does Reach's.

### Handed to ticket 14

Three constraints and one trap, posted as a comment on that ticket.

### Escalated to the map

`explain` and the dry-run verb are ruled **out of scope**, with revisit triggers. `--paths` ships
in M1 so the data exists for whoever writes `explain` later.

The documentation fog is narrowed, not graduated: a CI recipe per provider whose first line is
the fetch-depth setting, a TeamCity recipe passing `--base` explicitly, and `.reach/`'s lifecycle
stated as the caller's.

`CONTEXT.md` gains **Target branch**, because the glossary's `Baseline` entry already used that
phrase for the branch while this ticket used "target" for the solution. The positional gets no
name: `<SOLUTION|PROJECT>` on the command line, "the solution or single test project" in prose.
