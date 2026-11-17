# Test it or cut it: the claims no suite proves

Type: grilling
Status: open
Blocked by: (none — 11 resolved)

## Question

[Which suite proves each claim](11-which-suite-proves-each-claim.md) lists 28 claims in
`README.md`, `docs/adopting-reach.md` and `docs/report-schema.md` that no test proves, in four
groups. The gate rule says each one must be **tested or cut** before `0.1.0-alpha.1`. For each
claim, which one?

- **A. Documented, but Reach never does it** (three notice codes are never emitted; the
  README's bare `--filter` line). The choice is to implement it or to cut it from the docs.
  Acceptance row 18 expects `recompilation-widening`, so that row depends on this answer.
- **B. Reach does it, but no test checks it.** Which suite gets the test: an acceptance row's
  expected result, or a CLI or unit test?
- **C. Prose that drifts from what is pinned.** Add a parity test (like `RegisterParityTests`
  and `WorkflowParityTests`) or cut the prose?
- **D. Claims about other people's tools.** These are outside the gate rule as written. Keep
  them as cited facts, or cut them?

Three side questions from that ticket:

- Does the expected result gain a **`reasons`** section, so `nothing-selected` rows can prove
  the three reason codes?
- Are **`limitations.md`** and the **`--help` text** docs whose claims are in the gate? (The
  help already disagrees with the schema about `--paths`, and lists `--list-unselected`, which
  the schema does not mention.)
- Does the claim-to-suite table in the spec stay hand-kept, or is part of it checked?
