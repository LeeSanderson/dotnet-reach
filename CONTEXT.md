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
The members a change added, altered or removed, computed by matching declared types
between the baseline and the working tree — keyed on name, never on file path, so a moved
type is the same type.
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
different identities. A generic definition and every instantiation of it are one
identity — the type arguments at a call site are not part of it.
_Avoid_: method key, symbol name, signature

**Edge provenance**:
Why an edge exists, in one of three classes. **Compiled**: read directly from an
instruction. **Synthesised**: invented by Reach where the control flow is certain but no
instruction expresses it. **Widened**: the implementation that runs is not knowable, so
every candidate gets an edge. Every edge carries it, so any selection can be explained,
the cost of widening can be measured, and narrowing has one class it may safely touch.

**Kernel method**:
The method a developer wrote, as distinct from the compiler-generated members that carry
its body — async and iterator state machines, lambdas, local functions. A change inside
any of them is a change to the kernel method.
_Avoid_: user method, original method, outer method

**Widening**:
Adding edges from a dispatch site to every implementation it could reach, because IL names
a base or interface member rather than the implementation that runs. Bounded by the
inferred receiver type.
_Avoid_: expansion, fan-out

**Inferred receiver type**:
The static type a dispatch site can be shown to hold, recovered from the instructions that
produced the receiver. It bounds widening without narrowing it: a static type constrains
what a receiver can hold, so no edge that could run is removed.
_Avoid_: declared type, receiver type

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
any loaded framework model — reflection, plugin loading, convention-based registration, or
a first-party member invoked only from outside the analysis scope. Blind spots cause
under-selection, so a detected blind spot is always reported, and widened wherever a
bounded widening exists.

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

**Whole-assembly widening**:
Putting every method of an assembly into the changed set and then walking back as normal.
The response to a change Reach can attribute to an assembly but not to a method.
_Avoid_: whole-project selection, which selects tests directly rather than adding roots

**Whole-type widening**:
Putting every surviving member of a type into the changed set and then walking back as
normal. The same operation as whole-assembly widening, one granularity finer.
_Avoid_: type-level selection

**Dialect**:
The filter expression grammar a particular test framework, framework version and runner
host accepts. One selection renders into as many dialects as the solution contains.
_Avoid_: filter format, syntax

**Shadow mode**:
Running the selection and then the whole suite anyway, recording every test that was
skipped but failed. The instrument that proves a selection safe on a real codebase.

## The report

**Report**:
The machine-readable record of one run — the canonical selection, why each test was
selected, what could not be analysed, and the run's own inputs. A product surface with a
versioned schema, not a log: a selection nobody can audit gets switched off.
_Avoid_: output, log, results, audit trail

**Outcome**:
What a run concluded, from a closed set: a selection was made, nothing was selected, there
were no changes to analyse, or the run failed. An empty selection and an empty change set
are different news and are never conflated.
_Avoid_: status, result, exit state

**Invocation**:
One rendered command that runs part of a selection — an argument vector, never a shell
string. A test project's selection is always a list of them: several when the selection
exceeds the command-line ceiling, one when the whole project runs, and **none** when nothing
was selected, because an empty filter runs everything.
_Avoid_: command, filter, argument string

**Path class**:
How much to trust one test's selection by one change: the weakest edge provenance on the
strongest path between them. A test reachable by any fully compiled path is `compiled` even
if a widened path also exists; a test reachable only through widening is `widened`, which is
where over-selection is plausible and the only class narrowing may ever touch.
_Avoid_: confidence, path provenance, edge class

**Notice**:
One thing a run has to disclose — an unmodelled framework, a detected blind spot, a
deliberate widening, a fact about the environment. Identified by a stable code that is never
renamed, so it can be documented once and depended on. Every notice announcing a blind spot
has an entry in the limitations register.
_Avoid_: warning, diagnostic, message, error

**Limitations register**:
The catalogue of accepted holes — each with its direction of failure, whether Reach can
detect an instance of it, and its upgrade path. What makes named under-selection an honest
position rather than a silent one.
_Avoid_: known issues, caveats
