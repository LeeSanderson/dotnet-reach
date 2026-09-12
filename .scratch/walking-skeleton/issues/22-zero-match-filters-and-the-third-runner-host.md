# Zero-match filters and the third runner host

Type: grilling
Status: open
Blocked by: (none)

## Question

Graduated from [CI for the Reach repository itself](21-ci-for-the-reach-repository.md), which
found this while establishing what Reach's own test suite runs on and declined to absorb a
product decision into a CI answer.

**Under Microsoft.Testing.Platform, a filter that matches zero tests exits 8.** Verified
empirically on MTP 2.3.3 (`xunit.v3` 4.0.0): it fires on *"filter matched nothing"* as well as on
*"project has no tests"*, and the `--zero-tests-policy` default of `allow-skipped` rescues only the
all-skipped case, not a bad filter. `--ignore-exit-code 8` is the documented escape. Details in
[research/21-xunit-v3-mtp.md](../research/21-xunit-v3-mtp.md).

Reach is a tool whose entire output is test filters, and `xunit.v3` 4.0.0 on .NET 10 makes MTP
mandatory rather than optional — `dotnet test` with no `global.json` runner setting is a hard build
error. So the share of consumers this applies to is growing, not shrinking.

**Is this a real hole, or one [the report contract](07-the-report-contract.md) already closed?**
That ticket keyed invocations on (test project, target framework) and made an empty selection emit
**zero invocations**, so a consumer following the report never runs a zero-match filter and never
sees exit 8. The residue is the consumer who wires one filter solution-wide instead — which the
report's shape discourages but cannot prevent. Establish whether any path through Reach's own
design can render a filter that matches nothing, and if the answer is no, say so explicitly rather
than leaving it implied: the claim that this is unreachable is the whole mitigation.

**The inversion is the more interesting half, and may be an asset rather than a hazard.** A rendered
filter that fails to match a test the report says *was* selected is currently a **silent
under-selection** — the worst failure class in the map, and the one
[ADR-0008](../../../docs/adr/0008-m1-accepts-named-under-selection.md)'s bargain is built around
naming. Under MTP that same failure is a red build. Decide whether Reach should lean on this: is
"the host will catch a rendering bug for us" a property worth designing for, testing for, or
documenting — or an accident that must not become load-bearing, since VSTest and the direct
executable do not share it?

**What does this do to the fixture catalogue's second assertion?** [The fixture
catalogue](11-fixture-catalogue.md) made *"an empty selection emits zero invocations, not an empty
filter string — and exit code 0 in every host"* one of four mandatory integration assertions. It was
written before anyone knew a host exits 8. The assertion is probably still right and its
justification has changed — but restate it so the test asserts what is actually true, and consider
whether a fixture for the zero-match case itself now earns its place.

**Is the built executable a third dialect?** The glossary defines **dialect** as the grammar *"a
particular test framework, framework version and runner host accepts"*, and there are now three
hosts for xUnit v3, not two:

| Host | How reached | Filter option | Zero matches |
|---|---|---|---|
| MTP under `dotnet test` | `global.json` runner setting | `--filter`, `--filter-query`, `--filter-class` | exit 8 |
| The built executable | run directly; **not** MTP unless `UseMicrosoftTestingPlatformRunner=true` | `-filter`, `-class` | exit 0 |
| VSTest | only via `xunit.v3.mtp-off` | VSTest syntax | exit 0 |

[Filter dialects and runner detection](01-filter-dialects-and-runner-detection.md) resolved the
dialect question before this was known. Decide whether M1 renders for the direct-executable host at
all — a consumer running `./MyTests.exe` rather than `dotnet test` is plausible for xUnit v3, since
v3 projects are executables by design — or whether it is named as out of scope with a notice when
detected. Note the leading-hyphen difference is not cosmetic: `-filter` and `--filter` are different
switches on different hosts, so a dialect rendered for the wrong one is a wrong answer, not a
syntax error.

**Does anything here reach the PRD?** If the answer changes the report contract or adds a notice
code, check §4.3 and route any contradiction to [PRD amendments](06-prd-amendments.md) rather than
editing the PRD here.

Resolve at 80/20, per the map's Notes. The correctness rule outranks it: where a choice is
genuinely uncertain, take the one that widens.
