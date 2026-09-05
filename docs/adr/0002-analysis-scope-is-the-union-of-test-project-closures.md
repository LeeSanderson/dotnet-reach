# Analysis scope is the union of test-project closures

Analysis scope is every assembly in the transitive project closure of some test project.
Code outside every test closure cannot execute in any test process, so a change to it
cannot affect a test; code inside one can, whether or not anything depends on the project
that changed. Defining scope this way — rather than as "every assembly in the solution" —
is what makes it legitimate to point Reach at a single test project and analyse only that
project's closure.

## Considered options

The rejected narrowing is the changed-side one: analysing only the projects that
transitively depend on the project that changed. It fails because callers reference the
interface, not the implementation, so a caller's project is not a dependent of the
implementation's project and gets excluded despite being affected. PRD §8 calls this "the
most attractive available mistake"; it remains forbidden. Test-side narrowing is a
different operation and follows runtime executability, which is sound.

## Consequences

One hole remains, a blind spot rather than a flaw in the reasoning: assemblies loaded by
reflection or configuration, where code runs from an assembly nothing references.

Closures are derived from project references in the project files rather than from
assembly references in metadata, because the compiler omits references to assemblies whose
types are never named — so the metadata closure is narrower than the real one.

An earlier revision of this ADR claimed `ReferenceOutputAssembly=false` produced a copied
output with no metadata reference, and cited that as a second hole. That is wrong: it
produces neither a metadata reference nor a copy. `Private=false` is the separate switch
governing content copying. Such a reference expresses build order, not runtime
executability, so excluding it from the closure would be safe — deriving closures from
project references includes it anyway, which errs toward widening.

A pipeline that runs Reach against a single test project must still restore the whole
solution's output, or analysis scope cannot be established and Reach must error rather
than answer narrowly.
