# A solution wins over a project in target discovery

When Reach is invoked with no positional argument, it looks in the current directory only
and applies this rule:

- Exactly one `.sln`/`.slnx` present — **that solution wins, whatever project files sit
  beside it**.
- More than one solution — usage error, exit 1, naming what it found.
- No solution and exactly one project — that project.
- No solution and more than one project — usage error, exit 1.
- A `.slnf` solution filter — usage error, exit 1, naming the reason.

This deliberately diverges from MSBuild, and the divergence is the point of the record.

## What MSBuild actually does

MSBuild resolves the no-argument case in `XMake.ProcessProjectSwitch`, which `dotnet build`
and VSTest-mode `dotnet test` both delegate to; MTP-mode `dotnet test` carries a copy in the
SDK. Its rule for one solution beside one project is to **compare their base names**: equal
and the solution is used, different and it errors with MSB1011. With one solution and two
projects it errors outright.

Two footnotes for anyone checking this. The Learn page for MSB1011 states that "if a solution
file and multiple project files are found in the same folder, the solution is built" — that
documentation is wrong, and both the source and a live SDK disagree with it. And `.slnx`
participates on the same footing as `.sln` only from MSBuild 17.13 / SDK 9.0.200, where the
glob widened from `*.sln` to `*.sln?`; earlier SDKs cannot see a `.slnx` at all.

## Why Reach diverges

For a build, a solution and a project are two ways of naming work. For Reach they are two
different **analysis scopes**: a solution means the union of every test project's transitive
closure (ADR-0002), a `.csproj` means one project's closure. Choosing the project is
therefore choosing the *narrower* scope, and a narrower scope is an under-selection —
delivered silently, with a report that looks entirely plausible.

The map's standing rule settles it: where a decision is genuinely uncertain, take the option
that widens the selection. Faithfulness to a convention almost nobody knows is not worth a
class of silent under-selection, and Reach's rule is the more permissive of the two in every
case where they differ — it answers where MSBuild would error, and never the reverse.

## The boundary this draws

Reach mirrors `dotnet build`'s **option spellings** exactly — same long names, same short
forms, same semantics — so a pipeline author transfers what they already know and Reach never
invents a synonym for a concept the SDK has already named. That rule governs spelling. It does
not govern **resolution semantics**, where the correctness rule outranks the convention. This
ADR is the first application of that boundary; ticket 14 inherits it.

## Why `.slnf` is rejected rather than honoured

A solution filter is a declared *subset* of a solution's projects. Honouring one would shrink
the analysis scope by exactly the mechanism ADR-0002 forbids — under-selection wearing a
configuration file, and invisible in the report because the filter would look like the
solution. Rejecting it with a message that names the underlying `.sln` is honest and costs one
line of documentation. Silently expanding a filter to its full solution was the alternative;
it was rejected because a caller who passes a filter has stated an intent Reach would then
ignore without saying so.

## Consequences

The divergence has to be documented wherever discovery is described, or a future reader will
find MSBuild's base-name rule and "fix" Reach to match. The reversal that matters is not
adopting MSBuild's error case — that fails loudly — but flipping to "project wins", which is a
silent scope narrowing.

Reach reads solutions with `Microsoft.VisualStudio.SolutionPersistence` rather than MSBuild, so
its own `.slnx` support does not depend on the consumer's SDK version. The shelled-out
`dotnet build` in default mode does: a `.slnx` target on an SDK older than 9.0.200 fails in the
build, not in Reach.
