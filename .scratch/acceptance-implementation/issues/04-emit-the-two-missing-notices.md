# Emit `recompilation-widening` and `langversion-above-ceiling`

Status: ready-for-agent
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
