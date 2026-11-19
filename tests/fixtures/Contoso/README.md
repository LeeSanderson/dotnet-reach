# The fixture solution

**The fixtures are the specification of correctness**, and a fixture that omits a shape proves
nothing about it. This one carries what is a *project-file fact* rather than an IL shape —
everything that needs MSBuild to have run, and nothing that an in-memory Roslyn compilation
could have produced in milliseconds.

| Project | What it is here for |
|---|---|
| `src/Contoso.Core` | **multi-targeted** (`net8.0;net10.0`), so discovery resolves two assembly instances and rendering has to pin `-f` |
| `src/Contoso.Generator` | referenced with `ReferenceOutputAssembly="false" OutputItemType="Analyzer"`, so it is in the closure and **not** in the expected assembly set |
| `tests/Contoso.Tests.Xunit` | the equality dialect |
| `tests/Contoso.Tests.NUnit` | the **`~` dialect**, with a parameterised test and a `MyTest`/`MyTest2` pair — the one fixture standing between the design and a silent under-selection |
| `tests/Contoso.Tests.Unrecognised` | a test project on a framework Reach does not recognise, so whole-project selection has something real to fall back on |

**No acceptance test runs on the copy built in place.** The fixture projects are part of
`Reach.slnx`, so CI builds them here to hold them to the coding standard. The acceptance tests
use a separate copy: `FixtureSolution` copies only the files git tracks into a fresh temporary
directory, runs `git init` and commits it as the baseline, then applies the change under test.
A fixture committed in *this* repository has this repository's history, which is not a usable
baseline; and committing fixture *history* would make every test depend on a real commit graph
nobody can read.

The `bin/` and `obj/` that a build here leaves behind are ignored, never committed, and never
copied. The copy is what the tests build.
