# How a patch and its expected result read

Type: prototype
Status: resolved
Blocked by: (none)

## Question

How easy is an acceptance test to understand, given the patch format? The owner wants to *see*
this before choosing, so this is a prototype, not a decision on paper.

Take **one realistic acceptance test** — a change to a domain-layer method in a small layered
sample solution, selecting some domain and service tests and leaving an API test project
untouched — and lay it out in each candidate patch format:

- **A unified diff** (`change.patch`), applied with `git apply`.
- **Find-and-replace pairs** declared alongside the expected result (today's `Touch`).
- **Replacement files**: a folder of whole files copied over the sample solution.

Show each with its **expected result** beside it (selected sample tests, outcome, projects run
in full), so the owner judges the acceptance test as a whole: can a reader tell what changed,
and why exactly those sample tests are expected?

Also show, cheaply, how each format handles a **multi-file change** and a **file rename or
delete**, and what a reader sees when the sample solution has drifted and the patch no longer
applies.

The prototype is a throwaway under `.scratch/acceptance-layer/prototypes/`; it does not need to
build.

## Answer

**A patch is a plain unified diff, applied with `git apply`.** Chosen by the owner after
comparing all three formats on two acceptance tests: a one-line change, and a change plus a
file rename and a delete.

The checked-in form has **only file headers and hunks**:

```diff
--- a/src/Ledger.Domain/Invoice.cs
+++ b/src/Ledger.Domain/Invoice.cs
@@ -22,7 +22,8 @@ public sealed class Invoice(string customer, DateOnly due)
             return 0m;
         }
 
-        var monthsLate = daysLate / 30;
+        // A part month counts as a whole month.
+        var monthsLate = (daysLate + 29) / 30;
         return Total() * 0.015m * monthsLate;
     }
 }
```

- **No `diff --git`, `index <hash>..<hash>`, or file-mode lines.** The owner found the commit
  hashes too much detail. Checked: `git apply` applies the stripped form, and creates or
  deletes files from `/dev/null` alone.
- **The `---`/`+++` file header stays.** It is the only place a patch names the file it
  changes. `/dev/null` on one side means a file is created or deleted. Paths are relative to
  the sample solution's root, with git's `a/` and `b/` prefixes.
- **The `@@` hunk header stays, label included.** The line numbers are where `git apply` looks
  first, and it allows small offsets. The label after the second `@@` names the enclosing
  declaration, which tells a reader where the change is.
- **The harness never uses `--3way`.** Measured: `git apply --3way --check` exits 0 on a patch
  that conflicts, which would hide drift.

**Why not the others.** Find-and-replace (today's `Touch`) needs new `move` and `delete`
operations, and every `find` string must match exactly once. Replacement files **never fail on
drift**: copying a whole file silently undoes later edits to the sample solution, so the test
runs on code that is in no commit.

**What the prototype showed about drift.** A drifted patch fails loudly and names the file and
line (`error: patch failed: src/Ledger.Domain/Invoice.cs:22`), but not the reason. Drift
anywhere a hunk touches also fails, even a comment in a file being deleted.

**Passed on, not decided here:**
- Whether an acceptance test must give a `because` line → [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md).
- `git diff` cannot omit the header lines that the checked-in form leaves out, so whoever writes
  a patch needs a way to remove them → [Writing a patch and keeping it applying](08-writing-a-patch-and-keeping-it-applying.md).
- The map's outcome words (`ok` / `skip` / `failed`) are not `report.json`'s. The prototype's
  `expected.json` used the schema's words: `outcome` = `selected` · `nothing-selected` ·
  `no-changes` · `failed`, and `skip` is a per-project `mode`.

**Prototype:** branch `prototype/patch-format`, at `.scratch/acceptance-layer/prototypes/`. Its
`README.md` holds the side-by-side comparison and the real `git apply` output.
