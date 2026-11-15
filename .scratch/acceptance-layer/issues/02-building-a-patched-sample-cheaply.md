# What does a built, patched sample solution cost?

Type: research
Status: resolved
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

## Answer

Measured on SDK 10.0.403 with a warm NuGet cache; each figure is the median of 3 runs. Full
note: branch `research/building-a-patched-sample-cheaply`, at
`.scratch/acceptance-layer/research/02-building-a-patched-sample-cheaply.md`.

- **Reach does not need a baseline build.** It reads only binaries built from the working tree.
  One build of the patched tree gave identical reports for all 7 patches tried. Per test:
  Contoso 4.06 s, 8-project layered sample 6.17 s, including the Reach CLI.
- **An incremental rebuild is mostly fixed overhead.** Baseline 3.33 s / 4.37 s, rebuild after a
  method-body patch 1.90 s / 2.22 s, a no-op build 1.79 s / 2.34 s.
- **Copying a built tree to a new directory buys almost nothing.** The builds stay correct, but
  absolute paths in `obj/` and the PDBs make the first build at the new path recompile every
  project.
- **Restoring a clean built tree at the same path does stay incremental** (1.51 s / 1.75 s), but
  one directory serves only one test at a time. Restoring the sources without `bin/` and `obj/`
  leaves stale binaries, and Reach's correspondence check catches that (exit 5).
- **The correspondence check never failed falsely** on a reused tree: 36/36 runs, plus 240/240
  in parallel.
- **Parallel works with one directory per test.** 8 one-build tests went from 36.5 s → 13.6 s
  (Contoso) and 55.9 s → 24.0 s (layered). xUnit v3 runs one class's tests one at a time, so
  tests must be spread across classes or collections to run together.
- **Nothing in the .NET 10 SDK changes this.** The artifacts output layout still writes
  absolute paths.

Not measured: copying a built tree in parallel on the layered sample, or more than 8 tests at
once.
