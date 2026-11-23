# Emit `recompilation-widening` and `langversion-above-ceiling`

Status: resolved
Depends on: —
Spec: [§10.1](../../acceptance-layer/spec.md#101-implemented)

## Goal

Two notice codes the docs promise are catalogued but never emitted. Make Reach emit them.

## Scope

- **`recompilation-widening`**: whenever `RecompilationWidening.From` widens anything, raise the
  notice, with the kind and `data` the schema's index and the register entry
  ("Recompilation widening's blast radius") describe. One notice per run or one per triggering
  change: follow what `docs/report-schema.md` says, and if it says neither, choose one per triggering
  change (it names what caused the blast radius) and update the schema line.
- **`langversion-above-ceiling`**: raise the notice from the existing `ExceedsCeiling` check when a
  project declares a `LangVersion` above Reach's ceiling. **Reported, not widened** (walking-skeleton
  spec §9.5). Nothing about selection changes.
- In-memory tests that make Reach emit each code and assert it, next to the existing
  `RecompilationWideningTests` and `ParseHealthTests`.
- Check `docs/report-schema.md` and `docs/limitations.md` still describe both correctly. The
  example report does not change (no example run triggers either).

## Acceptance criteria

- Each code has a test that makes Reach emit it and asserts the code, kind and `data`.
- No selection result changes anywhere in the suite.
- `RegisterParityTests` still passes.

## Out of scope

Acceptance rows 18 and 19, which expect the notice (ticket 11). The notice-index parity test
(ticket 15).
## Comments

**Done.** Both codes are now emitted; no selection result changed.

- **`recompilation-widening`**: `RecompilationWidening.Widened.ToNotice()`, raised by
  `RootSets.ChangesFrom` (which now takes the selector's notice list, the same pattern as
  `TestEnumeration.Enumerate`) once per triggering change. Kind `widening`; `data.members` names
  the trigger (same spelling as its forward-change entry), `data.projects` the widened projects.
  The schema said neither per-run nor per-change, so per change it is, and the schema line now
  says so.
- **`langversion-above-ceiling`**: `ProjectFile` gained an optional `LangVersion` (read from the
  project, else the nearest `Directory.Build.props`). `ParseHealthCheck.AboveCeilingNotice`
  builds one `environment` notice listing every in-scope project above the ceiling in
  `data.projects`; `AnalysisScopeResolver` raises it. Reported only. The schema's `data` column
  for it changed from `—` to `projects`.
- Tests: two in `RecompilationWideningTests` (through `RootSets.ChangesFrom`), two in
  `ParseHealthTests`, two in `AnalysisScopeTests` (reading the property from disk end to end).
- `docs/limitations.md` already described both correctly; unchanged.

**Finding.** The `langversion-above-ceiling` check only fires on a numeric `LangVersion` Roslyn
cannot parse or that exceeds `Preview`'s effective version. With `Preview` as the ceiling, any
version this Roslyn knows is at or below it, so in practice the notice fires only for a number
newer than the shipped Roslyn (as intended), and never for a conditioned or `$(...)` value, which
is read literally and ignored as non-numeric.
