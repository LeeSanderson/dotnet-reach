# Where the acceptance tests run

Type: grilling
Status: resolved
Blocked by: (none — 02 resolved)

## Question

Do acceptance tests stay in `Reach.Tests` (filtered by namespace, as today) or move to their
own test project? How does a developer run the fast suite without them, and how does CI run
them — every push, on both Windows and Linux, as a separate job? Is there a time budget the
layer must stay inside as it grows, and what happens when it is exceeded?

Decided on the build-cost facts from
[What does a built, patched sample solution cost?](02-building-a-patched-sample-cheaply.md).

## Answer

Settled with the owner over two rounds of grilling.

- **They stay in `Reach.Tests`**, in the `Reach.Tests.Acceptance` namespace, as
  [How an acceptance test is declared](04-how-an-acceptance-test-is-declared.md) already lays
  out. A separate test project was rejected: it would split the harness helpers and reopen that
  layout for nothing but an unfiltered fast run.
- **Plain `dotnet test` runs everything, acceptance tests included.** There is no blessed
  "fast suite" command and no opt-in flag. A developer whose change has a small blast radius
  narrows the run with an ordinary filter for the area they touched (e.g.
  `dotnet test -- --filter-namespace "Reach.Tests.Graph*"`), on their own judgement. The
  contributor docs say so. Running Reach on its own repository is not that fast path: every
  acceptance test drives the whole pipeline, so almost any change to Reach's code selects the
  whole layer, which is correct.
- **CI runs them on every push to main and every pull request, on both Windows and Linux**, as
  a **separate step in the existing matrix job**, sharing its one build:
  1. `dotnet test ... -- --filter-not-namespace "Reach.Tests.Acceptance*"`
  2. `dotnet test ... -- --filter-namespace "Reach.Tests.Acceptance*"`

  A failing step names the failing layer. A separate job (a second restore and build) and a
  main-only or nightly run were rejected. The OS matrix matters most here, for the reason
  `ci.yml` already gives: path comparison is where Windows and Linux differ.
- **No time budget.** The acceptance step gets a generous `timeout-minutes` (about 20), with a
  comment saying it guards against a hung build and is not a budget. A hard timing limit was
  rejected as flaky on shared runners, and a soft stated budget as unneeded.
- **Parallelism is not decided here.** It belongs to
  [How the harness builds each acceptance test](09-how-the-harness-builds-each-acceptance-test.md),
  now as a question of the developer's wait rather than a limit.
