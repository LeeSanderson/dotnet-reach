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
| `tests/Basics.Tests` | `xunit.v3` 4.0.0, the tests Reach selects from |
| `global.json` | the testing-platform runner setting, and nothing else that changes the build |

**Each row's patch touches types that no other row's tests reach**, so a type here belongs to one
row:

| Type | Row |
|---|---|
| `Discount`, `Basket` | 1: a body change selects exactly the tests that reach it |
| `Initials` | 2: a test whose own body changed is selected |
| `Rounding`, `RoundingChecks` | 3: an existing non-test method that gains `[Fact]` is selected |
| `IShippingRate`, `FlatRate`, `WeightRate`, `Shipping`, `Label`, `UpperLabel`, `BracketLabel`, `Printer` | 4: interface and virtual dispatch select every candidate |
| `Thermometer`, `Readings` (in `Basics.Tests`) | 5: a deleted method widens its declaring type |
| `Tally` | 6: a move to another directory, same namespace, widens the type |

## Gap-shaped members

None yet. Known-gap tests depend on the members listed here, and gate patches never touch them.

## Building it

The projects are in `Reach.slnx` under `/samples/`, so the main build holds them to the coding
standard. Their tests are never run by `dotnet test`; they run only through Reach, inside an
acceptance test, over a copy of the files git tracks here.
