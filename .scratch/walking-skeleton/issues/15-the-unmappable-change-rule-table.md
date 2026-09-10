# The unmappable-change rule table

Type: grilling
Status: resolved
Blocked by: (none)

## Question

Charting settled the *tiers* by which an unmappable change resolves, and the third tier —
a changed file that no debug symbols reference — was left as "a rule table". Its rows were
never written. This ticket writes them.

Every row must state what it selects and which direction it errs in. A row that errs toward
selecting nothing is a correctness bug (PRD §8).

**Candidate rows:**

- `.csproj`, `Directory.Build.props`, `Directory.Build.targets`,
  `Directory.Packages.props` — a change here can alter compilation of one project or of
  every project in the tree.
- `global.json` — an SDK change, so every assembly in the solution may compile differently.
- `appsettings.json`, `.resx`, and other content copied to output — affects runtime
  behaviour without affecting compiled output, which is the case the tier system explicitly
  does not cover.
- `.editorconfig` — can alter analyser severity and therefore whether a build succeeds.
- Lock files, `nuget.config`, and package version changes.
- Files no rule matches. The default row, and the one that matters most.

**Source generators and analysers get their own treatment**, and
[Solution and project-file parsing](02-solution-and-project-file-parsing.md) showed the
detection is harder than charting assumed. `OutputItemType=Analyzer` is conventional, not
prescribed — the Roslyn cookbooks never mention it — and generators also arrive as a
`PackageReference` or a bare `<Analyzer>` item with no `ProjectReference` at all. So:

- How does Reach identify an in-repository generator or analyser project?
- What happens when it cannot? The safe answer is whole-suite selection, which is also the
  answer that makes Reach useless if it fires often.
- A generator edge is a build-order and compilation-input edge, not a runtime call edge.
  Does it belong in the call graph at all, with its own edge provenance (ADR-0004), or is
  it a separate relation the selection consults?

**The framing question underneath all of it:** how much of a real pull request lands in
this tier? If a typical PR touches a `.props` file and that selects everything, Reach saves
nothing regardless of how good the graph is. The rule table's rows decide whether the tool
works in practice, which makes this less of a detail than it looks.

## Answer

Six rows. The framing question is answered by **directory containment**, which is what keeps
the table from degenerating into "select everything" — the property that would have made the
tool worthless regardless of graph quality.

### The table

Every row states what it selects and which direction it errs. All widening is
**whole-assembly widening**, so the reverse walk runs normally afterwards.

| # | Change | Selects | Errs |
| --- | --- | --- | --- |
| 1 | `.csproj` | that project's assembly instances | over |
| 2 | `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig` | every in-scope project **at or below that file's directory** | over |
| 3 | `global.json`, `nuget.config`, lock files, and any solution file | every in-scope project | over |
| 4 | A generator or analyser project (detected, see below) | every project that consumes it | over |
| 5 | Content copied to output — `appsettings.json`, `.resx`, and anything else | its containing project | over |
| 6 | **Default: no other row matched** | nearest ancestor project, if any; otherwise **nothing**, with a notice | **under** |

**Directory containment is the load-bearing idea.** A `Directory.Build.props` beside one
service's projects widens that service, not the solution — and since MSBuild's own props
discovery walks *up* from each project, containment is not a heuristic, it is the same rule
the build itself uses. Only row 3 is genuinely solution-wide, and those files are genuinely
solution-wide in effect.

**No row needs to add dependents**, and this is worth stating because it looks like an
omission. Whole-assembly widening puts every method of the assembly into the changed set, and
reverse reachability then walks *backwards* to tests — so everything downstream of the widened
assembly is already reached. Adding the project graph's dependents would be a second, redundant
expansion. (The exception is [ADR-0014](../../../docs/adr/0014-removals-and-constant-changes-widen-transitive-referencers.md)'s
recompilation widening, which exists precisely because inlining can *erase* the reference the
reverse walk would have followed. Different problem, different mechanism.)

### Row 6 is a named under-selection, and it is deliberate

The default row is the only one that errs toward selecting nothing, which PRD §8 calls a
correctness bug. It is taken anyway, as an owner decision under
[ADR-0012](../../../docs/adr/0012-m1-resolves-open-design-questions-at-80-20.md).

The alternative is whole-solution selection for any unrecognised file, which means **a README
change runs the entire suite**. That is not conservatism with a cost; it is a tool nobody keeps
switched on, and it fires on a large fraction of real pull requests. Rows 1–5 enumerate every
build-affecting file that lives at a repository root, so the residue genuinely is
documentation-shaped: `README.md`, `.gitignore`, CI YAML, licence files, editor settings.

Registered with direction *under-selection* and marked **fully detectable** — Reach knows
exactly when row 6 fired with no ancestor project, so the notice is precise rather than a
blanket disclaimer. The upgrade path is a configurable pattern list, deliberately not in M1
because there is no evidence yet about which files real repositories keep at their roots.

### Generator detection is conventional, and says so when it fails

Ticket 02 established that `OutputItemType=Analyzer` is a convention rather than a
prescription. Reach checks the conventional markers — a `ProjectReference` carrying
`OutputItemType="Analyzer"`, and a project setting `IsRoslynComponent` or
`EnforceExtendedAnalyzerRules` — and row 4 fires when they match.

**When they do not match, Reach does not fall back to whole-suite selection.** A generator
arriving as a `PackageReference` or a bare `<Analyzer>` item is invisible, and the honest answer
is a register entry, not a widening that fires on codebases with no generator at all. The
safe-looking answer here is the one that makes the tool useless, and it would fire most often on
solutions that have nothing to widen for.

**A generator edge is not a call edge.** It is a compilation-input relation, so it does not
enter the call graph and gets no [ADR-0004](../../../docs/adr/0004-call-graph-edges-carry-provenance.md)
provenance. It is consulted by the rule table, at the point a change is routed to a tier — which
keeps the graph's seven edge kinds intact rather than growing an eighth that means something
categorically different.

### How a change reaches this table

Unchanged from [ticket 04](04-change-set-edge-cases.md), and repeated because the table reads as
if it were the whole mechanism: routing is **per change, not per file**, and the results union.
This table is tier three. A change reaches it only after failing to map to a method and failing
to match any document in any assembly's debug symbols.

### Consequences for other tickets

- **[The limitations register](19-the-limitations-register.md)**: two entries — row 6's
  no-ancestor case (detectable), and undetected generators (undetectable).
- **[Fixture catalogue](11-fixture-catalogue.md)**: a `Directory.Build.props` under a
  subdirectory, asserting projects *outside* it are not selected. That negative assertion is the
  one that proves containment works rather than being aspirational.
- **[Write the spec](12-write-the-spec.md)**: the table verbatim, including the `Errs` column.
