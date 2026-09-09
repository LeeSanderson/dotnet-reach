# CLI surface

Type: grilling
Status: open
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
