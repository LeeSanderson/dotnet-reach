# What does a built, patched sample solution cost?

Type: research
Status: open
Blocked by: (none)

## Question

Today every Contoso acceptance test copies the fixture into a temp git repository, builds the
baseline, writes the change, and rebuilds — two full builds per test, nothing shared, about a
minute for 13 tests. A layer that grows with every feature and bug, over multi-project sample
solutions, cannot afford that linearly.

**Establish the facts** the harness design will need:

- The cost of a baseline build versus an **incremental rebuild** after a small patch, on
  Contoso and on a ~8-project layered solution (a throwaway one is fine).
- Whether one **baseline build can be reused** across acceptance tests — by copying the built
  tree (`bin/`, `obj/`) into each test's temp repository, or by restoring a pristine tree
  between tests — and whether MSBuild's incremental checks stay correct after a copy
  (timestamps, absolute paths in `obj/`, `project.assets.json`).
- Whether Reach's **source–binary correspondence check** passes on a copied-then-rebuilt tree.
  Ticket 19 found that editing without rebuilding is a correspondence failure, which is Reach
  being right; this checks the reverse — that a reused build does not trip it falsely.
- Whether acceptance tests over the **same** sample solution can run in parallel (xUnit
  collections, separate temp roots), and what that buys.
- Anything the .NET 10 SDK offers that changes the answer (e.g. artifacts output layout).

Record numbers, not opinions; the harness choice is a later ticket's.
