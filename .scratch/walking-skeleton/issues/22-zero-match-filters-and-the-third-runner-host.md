# Zero-match filters and the third runner host

Type: grilling
Status: resolved
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

## Answer

**The hole is real, it is not the one the ticket suspected, and closing it converts exit 8 from a
hazard into an instrument.**

Two fact-finds drove this; both ran in a scratch directory on SDK 10.0.303 against `xunit.v3`
4.0.0 (MTP 2.3.3), and every claim below is a reproduced command.

### The hole: a multi-targeted project needs a framework selector

[The report contract](07-the-report-contract.md) keyed entries on (test project, target framework)
and made an empty selection emit **zero invocations**. That closes emptiness completely. What it
never said is what distinguishes the two entries' *commands* — and nothing did. `dotnet test` runs
every target framework of a project, so a filter naming a test that exists under only one of them
makes the others exit 8:

```
$ dotnet test --filter-class "MultiTfm.Net10OnlyTests"
  ...\bin\Debug\net8.0\MultiTfm.dll  (net8.0|x64)  Zero tests ran   Exit code: 8
  ...\bin\Debug\net10.0\MultiTfm.dll (net10.0|x64) passed
Test run summary: Failed!   error: 1   total: 1   succeeded: 1
EXIT=8
```

This is precisely the case the (project, TFM) keying exists for — ADR-0006 makes the target
framework part of method identity, so two frameworks can legitimately select differently. Reach
would have rendered the correct answer and failed the build with it.

**Fixed by `-f <moniker>` on every project with more than one assembly instance** —
[ADR-0015](../../../docs/adr/0015-invocations-pin-the-target-framework-where-it-is-derivable.md).
`-f` and `--framework` both work; a built `.dll` path does not, because `--test-modules` is a glob
resolved under `--root-directory` and rejects absolute paths. A single-instance project has
nothing to cross-contaminate and gets no selector, which matters more than it sounds — see the
moniker section below.

### The claim the ticket asked for, stated explicitly

**No path through Reach's own design renders a filter that matches nothing.** With the selector
in place the enumeration is exhaustive:

- **An empty selection** emits zero invocations. Nothing is rendered at all.
- **A chunk** always carries at least one test, by construction.
- **A multi-targeted project** now pins its framework. *This was the live hole.*
- **A consumer who wires one filter solution-wide** — the residue this ticket named — is outside
  Reach's design, and **loud**: one project matching nothing fails the whole run with `error: 1`,
  and the per-module line names the offending assembly. Not silent under-selection.
- **A consumer who mixes hosts** is unreachable through Reach, because M1 renders only
  `dotnet test`. See the next section; this is not a coincidence.

The two residual ways to see exit 8 from a correct Reach invocation are both the consumer's own
choice: `--zero-tests-policy strict` when every selected test in a project is skipped (the default
`allow-skipped` exits 0), and a rebuild between Reach's run and the test step.

### Exit 8 is an asset — documented, not designed for

Because Reach never emits an invocation that can legitimately match nothing, **exit 8 from a Reach
invocation means the rendering matched nothing while the report claimed tests were selected. That
is a Reach bug and nothing else.** It goes in the exit-code section of `docs/adopting-reach.md`,
which [ticket 20](20-what-documentation-m1-ships.md) placed with the pipeline author, worded for
current MTP hosts rather than promised: [ticket 01](01-filter-dialects-and-runner-detection.md)
already recorded that zero-match handling moves to a run-level verdict on the .NET 11 SDK.

**Reach never emits `--ignore-exit-code 8` and the documentation never recommends it.** It does not
merely suppress the signal, it erases the evidence — the zero-match module's line changes from
`Zero tests ran` to `passed` and the annotation disappears from the summary entirely, so a consumer
who adds it can no longer diagnose a rendering bug from the output.

No limitations-register entry. The register catalogues accepted holes; this is the inverse of one.

### The third host is out, and that is what keeps exit 8 honest

**M1 renders for `dotnet test` only.** The direct executable is out of scope, with **no notice** —
it would fire on nearly every xUnit v3 project and mean nothing.

The framing that settles it: the host is not a property of the test project. Reach emits an argv,
so **the argv already is the choice of host**, and `dotnet test` is the one choice that is
determinable — the generated entry point dispatches on `--internal-msbuild-node`, which
`dotnet test` always passes, so it is MTP regardless of `UseMicrosoftTestingPlatformRunner`.

The ticket suspected the leading-hyphen difference was dangerous. It is, and worse than stated.
The two hosts' option-name sets intersect in exactly four names — `culture`, `debug`, `explicit`,
`filter` — and MTP normalises a single dash to a double, so `-filter` is accepted by **both hosts
in different filter languages, with no diagnostic either side**:

```
$ Single.exe -filter "/*/*/AlphaTests/*"          # native: xUnit query language
   Single  Total: 2, Errors: 0, Failed: 0                        EXIT=0
$ dotnet test -filter "/*/*/AlphaTests/*"         # MTP: VSTest syntax
   Single.dll (net10.0|x64) Zero tests ran                       EXIT=8
```

Severity is bounded — every valid query string begins with `/`, which never matches as a
`FullyQualifiedName~` substring, so the reinterpretation collapses to zero matches rather than a
plausible wrong subset. But it is **indistinguishable from a genuine zero match**. Every other
cross-host spelling is loud (native exit 3, MTP exit 5), though `dotnet test` swallows MTP's
`Unknown option` text even at `--output Detailed` and shows only `Zero tests ran`.

So rendering the native dialect would manufacture the exact artifact that makes exit 8 ambiguous,
and the adoption doc above would misattribute it to Reach. **The two decisions hold each other
up.** A consumer who prefers the executable ignores Reach's argv and either runs everything or
hand-renders from the report's selected-test list — over-selection or manual work, never
under-selection.

### The moniker is not in the assembly

The selector needs the project's **declared** target-framework string, and metadata does not carry
it. `net10.0`, `net10.0-windows` and `net10.0-windows10.0.19041.0` all stamp the identical
`TargetFrameworkAttribute(".NETCoreApp,Version=v10.0")`. `TargetPlatformAttribute` recovers the
platform but always with a version, so `net10.0-windows` reads back as `Windows7.0` and
reconstructs to `net10.0-windows7.0` — which `dotnet test -f` rejects, with an error naming the
wrong subsystem entirely (`global.json defines test runner to be Microsoft.Testing.Platform`,
exit 1). And the mapping is not injective: a project declaring `net10.0-windows7.0` produces
byte-identical attributes, so no reconstruction rule can be right for both.

**The rule adopted is metadata-only** — base moniker from `TargetFrameworkAttribute`, no
`TargetPlatformAttribute` means no suffix and an exact answer. Where a multi-instance project does
carry one, Reach emits a single project-wide `run-all` invocation and reports
`framework-selector-underivable`. Over-selection, named, with
`dotnet msbuild -getProperty:TargetFrameworks` as the upgrade path. Rejected: the output directory
name, which *is* exact under the default layout but reintroduces the path-trust
[ticket 14](14-assembly-discovery-under-ambiguous-output-layouts.md) removed and breaks in exactly
the ambiguous layouts that motivated reading metadata.

This is why "selector only where the project has more than one assembly instance" matters: a
single-targeted `net10.0-windows` test project — a WPF or WinForms suite, the common shape — never
needs a moniker and never degrades.

### Two notice codes

- **`framework-selector-underivable`** (`kind: widening`) — the project ran in full because Reach
  could not name its target frameworks on the command line. One new over-selection entry in the
  register.
- **`test-runner-not-configured`** (`kind: environment`) — an `xunit.v3` 4.0.0 project in a
  repository with no `global.json` runner setting. `dotnet test` is a hard build error there, so
  every invocation Reach emits is unrunnable and the error names nothing Reach-shaped. Both halves
  are free to detect: the MTP adapter is in the assembly's referenced identities, and `global.json`
  is a root file [ticket 15](15-the-unmappable-change-rule-table.md)'s rule table already reads.
  Reach still emits the invocations and does not stop — the rendering is correct, the repository is
  misconfigured.

The second earns a register entry after all, in the **Environment** section: ADR-0010's entry sits
there with the same shape, and the initial judgement that it needed none had not checked that
section.

### The PRD was checked and is clear

§4.3 already commits to *"complete argument vectors — not a filter string — for each (test
project, target framework) pair"* and *"Reach owns the whole command line rather than contributing
a fragment to someone else's."* The selector is that commitment made concrete; ticket 07
under-delivered against it. Nothing contradicts, so nothing routes to
[PRD amendments](06-prd-amendments.md). The one imprecision — §4.3's *"which runner host is
executing it"*, which frames the host as observed — is a shading rather than a contradiction, and
`CONTEXT.md` is the document identifiers and prose are written against, so the sharpening lands
there.

### Glossary

**Dialect** sharpened: framework and version are read from compiled output, the **runner host** is
chosen when Reach renders. **Runner host** added as a term in its own right, because the ticket's
own confusion ("three hosts for xUnit v3, not two") came from a definition that implied all three
were detected.

### Amends three resolved tickets

- **[The report contract](07-the-report-contract.md)**: the argv carries `-f`; `run-all` with zero
  invocations is now representable, carried by the first entry in sort order.
- **[Assembly discovery under ambiguous output layouts](14-assembly-discovery-under-ambiguous-output-layouts.md)**:
  read `TargetPlatformAttribute` too; metadata does not hold the declared moniker. Identity is
  unaffected and the scan-and-verify thesis stands.
- **[Fixture catalogue](11-fixture-catalogue.md)**: assertion 2 restated, its exit-code clause
  dropped as untestable, its justification gaining a second direction; one new **unit** assertion
  on the renderer, since a missing selector fails loudly and does not earn an integration slot.

No new tickets. [Write the spec](12-write-the-spec.md) is now unblocked.
