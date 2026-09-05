# Reach

Reach works out which tests a code change could possibly affect, by reading compiled
assemblies and walking the call graph backwards from changed code to tests. This is the
project's glossary: the vocabulary every issue, spec, ADR and identifier should use.

## Change

**Baseline**:
The commit a change is measured against. In CI this is the merge-base of the current
branch and the target branch, not the target branch's tip.
_Avoid_: base commit, parent, previous version

**Changed set**:
The methods a change added, altered or removed, computed by comparing method bodies
between the baseline and the working tree.
_Avoid_: diff, delta, dirty set

**Unmappable change**:
A change that cannot be attributed to any method — an edited project file, an SDK bump,
a resource, or a source generator whose output lives in assemblies it does not appear in.
Unmappable changes always widen the selection.
_Avoid_: non-code change, unknown change

**Join**:
The step that turns a changed declaration in source into an identity in the call graph.
Deliberately a seam: the mechanism is chosen at implementation time, not by the domain.
_Avoid_: symbol resolution, mapping, lookup

## Graph

**Call graph**:
A directed graph whose nodes are methods and whose edges mean "this method can invoke
that one". Built from compiled assemblies, never from source.

**Method identity**:
A method's node in the call graph, derived from metadata rather than from its name.
Two methods compiled from the same source under different target frameworks are
different identities.
_Avoid_: method key, symbol name, signature

**Edge provenance**:
Why an edge exists — a compiled call instruction, widening through an interface or a
virtual override, or later a framework model. Every edge carries it, so any selection
can be explained and the cost of widening can be measured.

**Widening**:
Adding edges from a dispatch site to every implementation it could reach, because IL
names the interface or base method rather than the implementation that runs.
_Avoid_: expansion, fan-out

**Narrowing**:
Removing edges or selected tests using evidence that a particular implementation cannot
run — mocked dependencies, container registrations. Forbidden until proven safe, because
it is the only operation that can cause under-selection.
_Avoid_: pruning, filtering, refinement

**Reverse reachability**:
Following call edges backwards from the changed set to every test method that could lead
to it. The core algorithm.
_Avoid_: impact analysis, backward slice

**Blind spot**:
A call edge that exists at runtime but appears in neither the compiled instructions nor
any loaded framework model — reflection, plugin loading, convention-based registration.
Blind spots cause under-selection, so a suspected blind spot always widens the selection.

**Framework model**:
A description of call edges a framework establishes at runtime rather than in compiled
instructions, such as `IMediator.Send` reaching a handler. Purely additive: a model may
add edges, never suppress one.

## Scope

**Analysis scope**:
The assemblies Reach reads to build the call graph — the union of every test project's
transitive project closure. Distinct from build scope, and never narrowed to the projects
that depend on the change.
_Avoid_: analysis set, graph scope

**Build scope**:
The projects Reach asks the build to produce. May be narrower than analysis scope.

**First-party assembly**:
An assembly built from source inside the working tree, identified by the source documents
its debug symbols point at. Everything else in the output — package dependencies,
framework assemblies — is not first-party and is not analysed.
_Avoid_: local assembly, our code, project assembly

**Project graph**:
The dependency graph between project files, derived from their project references.
Much coarser than the call graph, and available without compiling anything.

## Selection

**Selection**:
The set of test methods Reach decides to run for a change. The canonical form is
framework-agnostic; a filter is one rendering of it.
_Avoid_: test list, chosen tests, filter

**Over-selection**:
Running more tests than strictly necessary. Wasteful but safe.

**Under-selection**:
Failing to run a test that would have caught a regression. A correctness bug, never a
tuning issue.
_Avoid_: missed test, false negative

**Test method**:
The unit of selection. Individual cases of a parameterised test are never selected
independently of the method that declares them.
_Avoid_: test case, test, fact, theory

**Whole-project selection**:
Selecting every test in a project because Reach cannot analyse it — an unrecognised test
framework, an unparseable project graph, a package with no model. The standard response
to missing information.
_Avoid_: fallback, bail-out

**Dialect**:
The filter expression grammar a particular test framework, framework version and runner
host accepts. One selection renders into as many dialects as the solution contains.
_Avoid_: filter format, syntax

**Shadow mode**:
Running the selection and then the whole suite anyway, recording every test that was
skipped but failed. The instrument that proves a selection safe on a real codebase.
