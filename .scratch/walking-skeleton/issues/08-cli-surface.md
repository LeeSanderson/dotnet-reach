# CLI surface

Type: grilling
Status: open
Blocked by: 07 (03 resolved), and informed by 14

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
