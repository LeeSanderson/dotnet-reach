# The patch tool and the stripped-form guard

Status: resolved
Depends on: 02
Spec: [§3](../../acceptance-layer/spec.md#3-patches)

## Goal

A developer goes from an edited sample to a checked-in patch in one command, and a patch in the
wrong form cannot be checked in.

## Scope

**`tests/tools/patch.cs`**: a single-file C# app (`dotnet run tests/tools/patch.cs -- ...`),
**not** in `Reach.slnx`. Exactly spec §3.4:

- `save <Sample> <name> [--on <baseline>] [--force]`: diffs the uncommitted edits under
  `tests/samples/<Sample>/`, writes `tests/Reach.Tests/Acceptance/<Sample>/<name>.patch` in the
  stripped form (only `---`/`+++` headers and `@@` hunks; paths relative to the sample root with
  `a/` and `b/`), then reverts the sample **only after the write succeeds**. Refuses to overwrite
  without `--force`. With `--on`, the diff is against the sample with that baseline patch applied,
  and the revert restores the untouched sample.
- `apply <Sample> <name> [--on <baseline>]`: `git apply --3way` onto the working-tree sample, the
  baseline patch first when given.
- Created and deleted files work (`/dev/null` headers). Untracked new files in the sample are
  included in `save`.
- Clear errors for an unknown sample, a missing patch, and nothing to save.

**The guard**: a fast test in `Reach.Tests` (not in the acceptance namespace) that reads every
`.patch` under `tests/Reach.Tests/Acceptance/` and fails on any `diff --git`, `index` or file-mode
line, naming the file and the `save` command that fixes it.

Re-save row 1's hand-stripped patch with the tool, to prove the round trip leaves it unchanged or
only trivially different.

## Acceptance criteria

- `save` then `git apply` in a fresh copy reproduces the edit, for a modified file, a created file
  and a deleted file. Prove this with a test or a scripted check recorded in this ticket's comments.
  The tool is outside the solution, so a manual check recorded here is acceptable.
- `save` never loses edits: if the write fails, the sample is not reverted.
- `save --on` and `apply --on` work against a baseline patch.
- The guard test passes on today's patches and fails on a patch containing `index abc..def`.
- Works on Windows and Linux. CRLF in the working tree does not leak into the patch in a way
  `git apply` then rejects on the other OS. Check this, because the repository is CRLF (ADR-0017).

## Out of scope

A guard against a patch no test uses (ruled out). An apply-only check mode (ruled out). Generating
expected results (ruled out).

## Comments

- **Done.** `tests/tools/patch.cs` (file-based app, not in `Reach.slnx`):
  `dotnet run tests/tools/patch.cs -- save <Sample> <name> [--on <baseline>] [--force]` and
  `dotnet run tests/tools/patch.cs -- apply <Sample> <name> [--on <baseline>]`, from anywhere in the
  repository. `save` diffs in a throwaway index (`HEAD`, plus the baseline via `git apply --cached`,
  plus untracked files as intent-to-add), so the developer's index is never touched; it strips
  everything before each file's `---`, writes CRLF, and only then reverts the sample
  (`git restore --source=HEAD --staged --worktree` and `git clean -fd`, both scoped to the sample;
  ignored build output stays). Binary files, empty files and mode-only changes have no stripped
  form, so `save` refuses them before writing or reverting. The guard is
  `Harness/PatchFormTests.cs` over `Harness/PatchForm.cs`: it rejects `diff --git`, `index`, the
  four file-mode lines and the rename/copy/similarity headers, lists every offending line, and names
  the `apply` and `save --force` commands.
- **Scripted check, 29 of 29 passed** (Windows, git 2.45): the three clear errors and a missing
  baseline; modify + create + delete saved in one patch, with `/dev/null` headers, no extended
  headers, CRLF throughout, sample clean afterwards; `--force` refusal; that patch applied with plain
  `git apply` in a fresh copy as the harness does (autocrlf off, raw bytes) reproduces all three
  files byte for byte; `apply` restores the edit in the working tree and `apply` then `save --force`
  is a fixed point; a write that fails (target is a directory) leaves the edit in place; `save --on`
  leaves the baseline's edit out, restores the untouched sample, and says "nothing to save" when the
  edits are exactly the baseline; `apply --on` applies both; the pair applies harness-style.
- **Full suite:** 636 passed, 0 failed (Windows, Debug).
- **Row 1 re-saved with the tool:** byte-identical to the hand-stripped patch.
- **CRLF:** the edited file in the check was LF throughout (Git Bash `sed` drops the CRs), and the
  patch still came out CRLF with matching context, because `git diff` compares the clean (LF) form.
  `save` writes CRLF, which is what `eol=crlf` checks out on Linux too, so the harness's raw-byte
  apply sees the same bytes on both. `apply` feeds git the patch with LF endings, the form git
  compares inside this repository. Linux not run locally; the fast CI step runs the guard.
- **Finding: `apply --3way` cannot merge a stripped patch.** With no `index` line there are no blob
  ids, so git says "repository lacks the necessary blob" and falls back to a plain apply. A drifted
  patch fails outright, with no conflict markers to resolve. `apply` then says so and points at the
  context git searched for. The drift routine in spec §3.5 ("apply, resolve, save --force") works
  only for drift git can absorb by offset or fuzz. Otherwise the developer makes the change by hand
  and saves. The spec may want to say this.
- **Finding: file-based apps need two properties here.** `PublishAot` defaults to true, and the
  ILCompiler package it pulls in fails NuGet audit (NU1903, warnings as errors). `RootNamespace`
  defaults to `patch`, which trips IDE0130. Both are set with `#:property`. The coding-standard
  analyzers apply to the tool through `Directory.Build.props`, and it builds clean.
- **Finding for orchestration:** `-nodeReuse:false` breaks `dotnet test --project ...` (it reaches
  the test app, and zero tests run) and `dotnet build file.cs` (the file is read as a project). Use
  `MSBUILDDISABLENODEREUSE=1` alone for those.
- Applying a stripped patch that creates a CRLF file prints git's "trailing whitespace" warning
  for each added line. This is harmless: the file is applied as written.
