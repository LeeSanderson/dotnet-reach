# Assembly discovery under ambiguous output layouts

Type: grilling
Status: open
Blocked by: (none)

## Question

Charting decided assembly discovery scans output directories rather than asking MSBuild,
because it must work under `--no-build`.
[Solution and project-file parsing](02-solution-and-project-file-parsing.md) established
that the thing being scanned is more ambiguous than assumed.

**The same `OutputPath` value yields different layouts depending on where it was set.**
From the project file, target framework and runtime identifier segments are still appended
— `custom\out\net8.0\`. Set as an MSBuild global property, which is what `dotnet build -o`
forwards, they are not, because global properties cannot be reassigned during evaluation —
`custom\out\` flat. **Nothing on disk records which happened.** Artifacts output
(`UseArtifactsOutput`) is a third layout, in which a single-targeted project gets *no*
target framework segment while a multi-targeted one does. And relative `-o` is absolutised
against the CLI's working directory, not the project's.

So a scan cannot deduce the layout; it can only predict candidates and check.

**To settle:**

- What is the discovery algorithm — predict candidate paths per project, enumerate, and
  verify? What is the candidate set, and in what order are collisions resolved?
- **When is "assembly not found" an error?** PRD §8 says a missing assembly indicates a
  build problem and must be an error. But under a layout Reach mispredicted, absence means
  Reach is wrong, not the build. Erring toward error is safe — it stops the run rather than
  under-selecting — but a tool that errors on a legitimate layout is a tool nobody adopts.
- **Stale artefacts.** A previous build's output for a removed project, or an abandoned
  target framework, sits in the output directory indefinitely. What distinguishes a stale
  assembly from a current one, and does ADR-0003's checksum verification catch it or is a
  separate rule needed?
- Which layout inputs become command-line arguments (configuration, output root, artifacts
  path) versus detected? Note `--artifacts-path` must be cascaded to `--no-build`.
- Multi-targeted projects under artifacts output leave the outer-build path computed but
  **empty** — that must not read as a missing assembly.

The output of this ticket feeds the CLI surface (which of these are arguments) and the
report contract (how a discovery failure is reported and with which exit code).

## Comments

**From [CLI surface](08-cli-surface.md):** resolved, and it deliberately did *not* decide which
layout options exist — that stays here. What it did fix is the frame they land in.

- **Layout hints mirror `dotnet build`'s spelling exactly** — same long name, same short form,
  same semantics — so a pipeline author transfers what they know and Reach never invents a
  synonym for a concept the SDK has already named. This ticket may add options; it may not
  invent spellings. Note `--artifacts-path` is hyphenated and has **no short form** on any
  `dotnet` command.
- **`--output`/`-o` is reserved for build output** and can never mean "where Reach writes its
  files" — Reach's own directory is `--report-dir`, defaulting to `.reach`.
- **Layout switches must be Reach's own options, never passthrough tokens.** The CLI forwards
  everything after `--` to `dotnet build`, but scans the forwarded tokens for `-o`, `--output`,
  `--artifacts-path` and `-c` and refuses with exit 1. A forwarded `-o` would change the layout
  this ticket has to predict, through a channel Reach never read.
- **The mirror rule governs spellings, not resolution semantics.** Where a resolution rule could
  narrow scope, the correctness rule outranks the convention —
  [ADR-0011](../../../docs/adr/0011-a-solution-wins-over-a-project-in-target-discovery.md) is the
  first application, and this ticket inherits the boundary.

**A trap for the rendering side, found while checking option spellings:** MTP-mode `dotnet test`
has **no `-o` at all**, and spells `--output <Minimal|Normal|Detailed>` to mean *test output
verbosity*. VSTest-mode `dotnet test` and `dotnet build` both use `-o|--output` for a directory.
So an emitted `dotnet test` argv must never carry `-o` for an MTP host. Belongs to whoever owns
rendering rather than to this ticket, but it was found here and is easy to lose.
