# Assembly discovery under ambiguous output layouts

Type: grilling
Status: resolved
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

## Answer

**Reach does not predict layouts. It scans and verifies.** Owner decision, resolved under
[ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md).

The ticket asked for a candidate-path prediction algorithm with a collision order. That
question is not answered — it is **dissolved**. Ticket 02 established that the layout is
undecidable from disk; predicting it means enumerating every way MSBuild can be told where to
put output and hoping the list is complete. Searching for the assemblies and then proving each
one is the right assembly needs no such list.

### The algorithm

1. **Enumerate** `*.dll` under the target's directory tree — the solution's directory, or the
   project's for a single-project target — excluding `obj/`, `.git/`, `node_modules/`, `.vs/`
   and `packages/`.
2. **Prune by file name** against the expected assembly simple names, which come from the
   solution's project list ([ticket 02](02-solution-and-project-file-parsing.md), no MSBuild
   required). This is the cheap filter and it runs before anything is opened, which is what
   keeps a scan over a large repository affordable.
3. **Open** each survivor's metadata and debug symbols. **First-party** iff the symbols point
   at source inside the working tree — already the map's rule, now doing double duty.
4. **Read the target framework from `TargetFrameworkAttribute`** on the assembly itself, never
   from a path segment. This is the quiet payoff: the TFM is what the flat-layout, artifacts-output
   and `-o`-as-global-property cases all destroy on disk, and it was always available in metadata.
5. **Match** to the expected set of `(project, target framework)` instances — assembly
   instances, in [ticket 09](09-method-identity-and-performance-budget.md)'s sense.

Every case the ticket listed as ambiguous now falls out rather than needing a rule:
`OutputPath` set in the project file versus forwarded as a global property, artifacts output's
missing TFM segment for single-targeted projects, relative `-o` absolutised against the CLI's
working directory, and the multi-targeted outer build's computed-but-empty directory — which
cannot read as a missing assembly because no path is ever computed.

### "Assembly not found" is now unambiguously an error

The ticket's dilemma was that under a mispredicted layout, absence means Reach is wrong rather
than the build. **Scan-and-verify removes the horn:** the tree was searched, so absence is
absence. PRD §8's rule applies without qualification — a missing assembly for an expected
project instance is **exit 3**.

Two things are *not* missing assemblies, and both need saying so the error does not misfire:

- A project outside the analysis scope, per
  [ADR-0002](../../../docs/adr/0002-analysis-scope-is-the-union-of-test-project-closures.md) —
  not expected, so not an error.
- A project that produces no output assembly, which ticket 02 found is more common than
  ADR-0002 assumed: generator and analyser projects, and projects referenced with
  `ReferenceOutputAssembly=false`. Excluded from the expected set.

### Ambiguity errors, and says how to fix it

When two or more candidates survive for the same `(project, target framework)` — Debug and
Release both present, or `bin/` alongside an artifacts directory — Reach **errors with the
candidate list and names the option that disambiguates**, reusing **exit 3**, whose meaning
widens from "assembly missing" to "assembly discovery failed". Erring toward stopping is
PRD §8's direction, and unlike a mispredicted-layout error this one is always genuinely the
caller's to resolve.

Guessing was rejected. Newest-mtime is the obvious heuristic and it silently picks a stale
Release build over a fresh Debug one about as often as not, which converts a loud stop into
under-selection with no notice.

### Layout options demote to narrowing hints

`-c|--configuration`, `-o|--output` and `--artifacts-path` stop being layout *inputs* and
become **filters applied to scan results** — all optional, all spelled exactly as `dotnet build`
spells them, per ticket 08's mirror rule. Under scan-and-verify their only job is to break the
ambiguity above, which is why none of them is required for the common case. `--artifacts-path`
cascades to `--no-build`.

### Stale artefacts

Mostly free, and worth enumerating because each has a different reason:

- **Output for a project no longer in the solution**: matches no expected instance, ignored.
- **Output for an abandoned target framework**: its `TargetFrameworkAttribute` matches no
  expected instance, ignored.
- **A stale assembly whose source changed**: caught by
  [ADR-0003](../../../docs/adr/0003-verify-source-binary-correspondence-via-pdb-checksums.md)'s
  checksum verification — **exit 5**.
- **A stale assembly whose own source did not change but whose dependencies did.** The residue.
  Checksums pass because they only speak for that assembly's sources. Default mode runs the
  build, so it cannot arise there; under `--no-build` it is a **register entry**, direction
  under-selection, detectable only by comparing dependency timestamps, which M1 does not do.

### Consequences for other tickets

- **[Write the spec](12-write-the-spec.md)**: the algorithm above, exit 3's widened meaning,
  and the expected-set exclusions.
- **[The limitations register](19-the-limitations-register.md)**: the stale-transitive-dependency
  hole under `--no-build`.
- **[Fixture catalogue](11-fixture-catalogue.md)**: scan-and-verify makes layout fixtures cheap —
  the same built output relocated, rather than a project per layout — and it is the natural place
  to prove the ambiguity error fires.
- Rendering still owes the MTP `-o` trap above. Unowned by this ticket, carried to the spec.
