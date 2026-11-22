# The patch tool and the stripped-form guard

Status: ready-for-agent
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
