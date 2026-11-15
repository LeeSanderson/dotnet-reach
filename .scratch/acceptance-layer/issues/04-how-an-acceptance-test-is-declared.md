# How an acceptance test is declared

Type: grilling
Status: open
Blocked by: (none — 01 resolved)

## Question

Given the patch format chosen in [How a patch and its expected result read](01-patch-format-and-readability.md):
is an acceptance test **data** — one folder per test (patch, expected result, a short
README saying what it shows), all discovered and run by a single xUnit theory — or **code**,
one C# test method per acceptance test with its patch alongside?

Settle too: where sample solutions and acceptance tests live in the tree, how an acceptance
test names its sample solution, and the exact shape of the expected result's file.

## Comments

- **From [How a patch and its expected result read](01-patch-format-and-readability.md):** the
  patch is a plain unified diff (`change.patch`). Two questions the prototype raised belong
  here:
  - **Must an acceptance test give a `because` line?** That is a short free-text reason for
    its expected selection. In the prototype's rename-and-delete test, nothing in the patch
    explains why `Net_is_the_sum_of_the_lines` is selected. The reason is whole-assembly
    widening, from walking-skeleton spec §9.3.
  - **Use `report.json`'s vocabulary in the expected result.** The map's `ok` / `skip` /
    `failed` are not the schema's words. The prototype used `outcome`
    (`selected` · `nothing-selected` · `no-changes` · `failed`), full `display` names grouped
    by test project, and `runInFull`. Its `expected.json` is on branch
    `prototype/patch-format`.
- **From [Which capabilities the layer demonstrates, and which gate the release](03-capabilities-and-the-release-gate.md):**
  - The expected result gains **optional** sections: **will run**, **notices** (codes),
    **changes** (member + test count) and **tier** per change. A section that is given is
    **exact**. No exit code — acceptance tests stay in-process.
  - **Known-gap tests** must be declarable and visibly named as gaps (e.g.
    `Known_gap_interpolated_ToString_is_not_selected`), and kept out of the release gate — the
    declaration needs some way to say which batch a test is in.
