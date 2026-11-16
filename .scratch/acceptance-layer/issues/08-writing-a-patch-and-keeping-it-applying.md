# Writing a patch and keeping it applying

Type: grilling
Status: resolved
Blocked by: (none — 04 resolved)

## Question

A patch is a plain unified diff with only `---`/`+++` file headers and `@@` hunks
([How a patch and its expected result read](01-patch-format-and-readability.md)). `git diff`
always writes `diff --git` and `index` lines, and git has no flag to leave them out.

- **Writing one.** How does a developer go from an edited sample solution to a checked-in
  `change.patch`: a script, a harness mode, or by hand? Does the harness reject the header lines
  the checked-in form leaves out, or accept them?
- **Noticing drift.** A drifted patch fails with only the file and line
  (`error: patch failed: src/Ledger.Domain/Invoice.cs:22`). What does a failing acceptance test
  report on top of that, and is there a check that every patch still applies, apart from
  running the whole layer?
- **Fixing drift.** When a sample solution changes, what is the routine for regenerating the
  affected patches and for re-checking their expected results? Can an expected result ever be
  regenerated from Reach's output, or must it always be hand-written? The map makes it the
  oracle.

Depends on where acceptance tests live and how they are declared, from
[How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md).

## Comments

- **From [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md):**
  patches sit flat beside their sample's test class in `tests/Reach.Tests/Acceptance/<Name>/`,
  and a test names them explicitly, so one patch may be **shared** by several tests or
  **combined** with others (applied in order). Two things land here:
  - **A guard against a patch no test uses**, since nothing ties a file to a method by name.
  - **Combined patches must apply in sequence**, so drift checking must apply each test's list
    in its order, not each patch alone.

## Answer

Settled with the owner over two rounds of grilling.

### Writing a patch: a small tool

**`tests/tools/patch.cs`**, a single-file C# app outside `Reach.slnx`, run as
`dotnet run tests/tools/patch.cs -- <save|apply> <Sample> <name>`. `<Sample>` is the folder name
under `tests/samples/`; `<name>` is written without `.patch`.

- **`save`** takes the uncommitted edits to `tests/samples/<Sample>/`, writes
  `tests/Reach.Tests/Acceptance/<Sample>/<name>.patch` in the stripped form (paths relative to the
  sample root, `a/`/`b/` prefixes), then reverts the sample — only after the write succeeds, so
  nothing is lost. It refuses to overwrite an existing patch without `--force`.
- **`apply`** applies an existing patch to the sample in the working tree with
  `git apply --3way`, for fixing drift or starting a new patch from an old one. `--3way` is fine
  here because a human resolves the result; the harness itself still never uses it.

Rejected: writing patches by hand (fiddly), and a harness mode (authoring has nothing to assert).

### The stripped form is enforced

A **fast guard test** rejects any `.patch` containing `diff --git`, `index` or file-mode lines,
and its message names `save`. Accepting them would leave the readability choice from
[How a patch and its expected result read](01-patch-format-and-readability.md) as a convention
only.

### Patches are independent

**Every patch applies cleanly to the untouched sample by itself.** Combining patches in
`Apply(...)` still works, but only stacks changes; changes that depend on each other belong in
one patch. A rule in the spec, not a guard. It keeps `save` diffing against the sample alone and
drift fixable one patch at a time. This answers the comment above: there is no order to check.

### Noticing drift

A failed apply reports, on top of git's file-and-line:

1. the patch name and its position in the test's list (`2 of 3: dunning-reminder.patch`);
2. git's full `git apply --verbose` output, which shows the context it searched for;
3. one fix line: `dotnet run tests/tools/patch.cs -- apply <Sample> <name>`, resolve, then
   `save --force`.

**No separate check that patches apply.** Drift shows when the layer runs, which CI does on
every push ([Where the acceptance tests run](07-where-acceptance-tests-run.md)). An apply-only
mode (tests skipping after a successful apply) was considered and declined.

### Fixing drift

`apply`, resolve any conflict markers, `save --force`. Then re-read the `Because` of every test
that names the patch.

### Expected results are always hand-written

**Never generated from Reach's output.** A failure shows the section-by-section difference and
nothing more — no pasteable `Expected { ... }` literal, no approve/snapshot mode. Stale
expectations after a sample or patch change are caught because every given section is exact,
so CI goes red; no PR review rule is added.

### Ruled out

- **A guard against a patch no test uses** — the owner judged it unnecessary.
- Dependent patches, an apply-only check, generated expected results.

### Not changed

No `CONTEXT.md` change and no ADR: test infrastructure. **Independent patch** joins the map's
vocabulary. Nothing new to ticket; all of it feeds the spec. The harness-side reporting is passed
to [How the harness builds each acceptance test](09-how-the-harness-builds-each-acceptance-test.md).
