# How a patch and its expected result read

Type: prototype
Status: open
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
