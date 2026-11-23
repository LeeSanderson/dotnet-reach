# Basics

A small **sample solution** for Reach's acceptance layer. It carries every small row of the
release gate except the two that need the Frameworks sample's standing notices. See the
acceptance-layer spec, §2.3.

This folder is the baseline repository Reach reads in an acceptance test, so it holds the sample
and nothing else. The acceptance tests live in `tests/Reach.Tests/Acceptance/Basics/`, beside the
patches they apply.

| Project | What it is here for |
|---|---|
| `src/Basics.Core` | the production code, single `net10.0` |
| `src/Basics.Generator` | a stand-in source generator, consumed by `Basics.Core` as an analyser (row 12) |
| `src/Basics.Tools` | production code no test project references, so outside the analysis scope (row 29) |
| `tests/Basics.Tests` | `xunit.v3` 4.0.0, the tests Reach selects from |
| `global.json` | the testing-platform runner setting, and nothing else that changes the build |

**Each row's patch touches types that no other row's tests reach**, so a type here belongs to one
row:

| Type | Row |
|---|---|
| `Discount`, `Basket` | 1: a body change selects exactly the tests that reach it |
| `Basics.Generation.Marker`, and the generator's project file | 12: a generator change widens its consumers |
| `Receipt` (no test reaches it) | 13: formatting only; 15: a change reaching no test; 28: a change that breaks the build |
| `Voucher` (added by row 16's patch, so not in the sample) | 16: a new file git does not track |
| `Basics.Tools.Housekeeping` | 29: a change outside the analysis scope |
| this README | 14: a docs-only change |

## Gap-shaped members

Known-gap tests depend on the members listed here, and gate patches never touch them.

| Member | Known gap |
|---|---|
| `Handler<T>`, `Message`, `Foo`, `Bar`, and their test class `HandlerTests` | 24: a change reached only through `Handler<Foo>` also selects the users of `Handler<Bar>` |
| `Temperature` (its `ToString`), and its test class `TemperatureTests` | 25: a `ToString` reached only through string interpolation selects nothing |

## Building it

The projects are in `Reach.slnx` under `/samples/`, so the main build holds them to the coding
standard. Their tests are never run by `dotnet test`; they run only through Reach, inside an
acceptance test, over a copy of the files git tracks here.
