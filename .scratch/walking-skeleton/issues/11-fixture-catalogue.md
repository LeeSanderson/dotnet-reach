# Fixture catalogue

Type: grilling
Status: open
Blocked by: 04, 05

## Question

PRD §12 gives M1 the acceptance criterion "Correct on a sample repository" and nothing
more. This ticket turns that phrase into a list, because the fixtures *are* the
specification of correctness — a fixture that omits a shape proves nothing about it.

The charting session sketched the shapes a fixture solution must contain: interface
dispatch with two implementations; an abstract base with overrides; generics, including a
generic method used at two instantiations; `async`/`await` and an iterator; a lambda and a
local function; an explicit interface implementation; a multi-targeted project; a project
referenced with `ReferenceOutputAssembly=false`; a source generator project; a test project
in a framework Reach does not recognise, to prove the whole-project fallback fires; and a
change to a comment only, to prove it is *not* selected.

**To settle:**

- Is that list complete? The edge cases from
  [Change-set edge cases](04-change-set-edge-cases.md) and the node and edge definitions
  from [Generics, delegates and function pointers](05-generics-delegates-and-function-pointers.md)
  each imply fixtures that are not in it.
- One fixture solution containing everything, or several small ones? One is realistic and
  exercises scale; several are diagnosable when they fail.
- How does an integration test get a git history? A fixture committed in this repository
  has this repository's history, which is not a usable baseline. Constructing a temporary
  git repository per test is the obvious answer — confirm it, and decide how the fixture
  gets into it.
- What exactly is asserted: an exact expected selection, or that specific tests are in and
  specific tests are out? Exact assertions catch over-selection regressions and break on
  every fixture change.
- **The negative assertions matter most.** A test that must *not* be selected is the only
  kind that catches over-selection, and a test that *must* be selected is the only kind
  that catches under-selection. Both need to be explicit rather than emergent.
- Where does the boundary sit between in-memory compilation tests and fixture-solution
  integration tests? Anything testable in memory should be, since those run in
  milliseconds and the fixture builds do not.

**Required by the dialect research:** an NUnit parameterised test whose selection through
a rendered filter is asserted end to end. NUnit's VSTest `FullyQualifiedName` includes the
arguments, and method-level matching survives only via an adapter re-parse that several
supported configurations disable — so this is the one fixture standing between the design
and a silent under-selection. Ideally exercised under both the default configuration and
`UseNUnitFilter=false`.
