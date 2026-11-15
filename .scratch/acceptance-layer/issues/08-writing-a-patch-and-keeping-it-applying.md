# Writing a patch and keeping it applying

Type: grilling
Status: open
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
