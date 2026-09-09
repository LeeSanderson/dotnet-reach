# Per-host private filter channels, not .runsettings

A selection too long for a command line is delivered through **each runner host's own
private file channel** — `@file.rsp` for Microsoft.Testing.Platform, `@@ file` for xUnit v3's
native CLI, `--testlist=FILE` for nunit-console. Where a host has no private channel, the
selection is **split across multiple invocations**. Reach does not write a `.runsettings`
`<TestCaseFilter>` unless the caller explicitly hands it their runsettings path.

A future reader will find that VSTest's `.runsettings` `<TestCaseFilter>` has **no length
limit at all** — maintainer-tested at 3.5 MB of filter for a 65,000-test run — and will
reasonably wonder why the one unlimited hatch was passed over in favour of chunking. This is
why.

## The ceiling is the normal path, not an edge case

A `FullyQualifiedName=Acme.Orders.OrderServiceTests.SubmitRejectsEmptyBasket` clause runs
60–90 characters. Against `cmd.exe`'s documented 8,191-character limit that is roughly **100
selected test methods**; against `CreateProcessW`'s 32,767, roughly **400**. An ordinary
pull request on a large solution clears both. So this is not a guard for a pathological
input; it is the mechanism most real selections travel through, which is what makes the
choice worth an ADR.

## Why not .runsettings

Two independent defects, either of which alone would disqualify it as a default.

**A pre-existing `<TestCaseFilter>` is AND-ed with Reach's, on both hosts.** The consumer's
filter is typically an exclusion — `TestCategory != Integration` — so the intersection is
strictly smaller than Reach's selection. That is an **under-selection**, and it is silent:
the tests simply do not run, and nothing reports a conflict.

**Only one settings file can be passed.** `dotnet test --settings` and
`vstest.console /Settings:` take a single path, so a file Reach writes does not merge with
the consumer's — it *replaces* it, discarding whatever else that file configured.

Detection cannot rescue either. A runsettings file is chosen by the pipeline's own command
line or by an MSBuild property, and Reach is invoked as a separate step that never sees the
runner's arguments. This is the same blindness that forced NUnit selections to render with
`~` rather than equality: Reach cannot read the consumer's runsettings, and — worse — cannot
detect that it failed to.

Under the correctness rule, an undetectable under-selection is not a trade-off to be
measured. It is disqualifying.

## Why the private channels are different

`@file.rsp`, `@@ file` and `--testlist` are all **Reach's own files, passed as arguments**.
They occupy no channel the consumer configures, cannot merge with consumer state, and cannot
be silently intersected with anything. They also carry no length limit worth budgeting for.

xUnit's native CLI requires that `@@ file` be the *only* argument on the command line, which
would normally be prohibitive — it is affordable only because the report contract already
emits complete argv vectors rather than filter fragments, so Reach owns the whole command
line anyway.

## The one degraded host

`dotnet test` in VSTest mode has no working response file: it does not recognise one for a
DLL-based run, and `dotnet vstest` expands the file back onto the command line,
reintroducing the limit. The issue asking for it is closed as not planned.

That host chunks. The cost is real and scales the wrong way — N process starts and N test
discoveries over the same assembly, worst for the largest selections — and it is accepted
because the alternative is an undetectable under-selection.

`vstest.console.exe` invoked directly *does* accept `@file`. Rejected: locating it means
discovering a Visual Studio installation or a test-platform package layout, which is a
larger and less reliable problem than the one it solves.

## The explicit opt-in, and its downgrade

A caller who hands Reach their runsettings path has supplied the fact Reach could not
discover, so Reach may merge and emit a combined file. If that file already contains a
`<TestCaseFilter>`, Reach **refuses to render a filter for that project and downgrades it to
whole-project selection**, because AND-ing with a filter whose intent it cannot know is the
under-selection above, and the standing rule is to widen when uncertain.

## Consequences

Reach must model **four** delivery mechanisms rather than one, and the report's per-project
entry has to name which was used — the argv is otherwise inexplicable.

Chunking makes an invocation list, not an invocation, the unit a consumer executes. The
report contract therefore makes `invocations` an array for every project, which has the
useful side effect that an empty selection is an empty array rather than a filter string
that would run everything.

Every chunk and every runsettings downgrade emits a notice, so a consumer paying the
chunking cost or losing filter precision can see it rather than inferring it from a slow
pipeline.
