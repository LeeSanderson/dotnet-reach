# The tier ladder and the unmappable-change rule table

Status: ready-for-agent
Depends on: 06, 08, 10
Spec: [§10](../../walking-skeleton/spec.md#10-routing-an-unmappable-change-the-tier-ladder-and-the-rule-table)

## Goal

Route every change that does not map to a member, without letting the table degenerate into
"select everything".

## Scope

**Routing is per change, not per file, and the results union.** Per-file gating is the only
version that can return less than the sum of its parts, and the case it loses is the ordinary
refactoring commit: a deletion sharing a file with an edit, where the edit keeps the file on tier
1 and the deletion resolves to nothing.

| Tier | Condition | Response |
|---|---|---|
| 1 | the change maps to a member | **join** it and walk (ticket 10) |
| 2 | a changed file with no changed member, referenced by some assembly's debug symbols | **whole-assembly widening** on every assembly whose symbols list that document |
| 3 | no symbols reference the file | the rule table below |

Tier 2's lookup is the inverse of ticket 08's document enumeration — build it once, do not
re-enumerate.

## The rule table

All widening here is **whole-assembly widening**, so the reverse walk runs normally afterwards.
Implement the `Errs` column as part of the record on each change, because ticket 12's forward
change list surfaces it.

| # | Change | Selects | Errs |
|---|---|---|---|
| 1 | `.csproj` | that project's assembly instances | over |
| 2 | `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `.editorconfig` | every in-scope project **at or below that file's directory** | over |
| 3 | `global.json`, `nuget.config`, lock files, and any solution file | every in-scope project | over |
| 4 | a generator or analyser project (detected, below) | every project that consumes it | over |
| 5 | content copied to output — `appsettings.json`, `.resx`, anything else | its containing project | over |
| 6 | **default: no other row matched** | nearest ancestor project, if any; otherwise **nothing**, with a notice | **under** |

**Directory containment is the load-bearing idea and is what stops the table degenerating.** A
`Directory.Build.props` beside one service's projects widens that service, not the solution — and
since MSBuild's own props discovery walks *up* from each project, containment is not a heuristic,
it is the same rule the build itself uses. **Only row 3 is genuinely solution-wide**, and those
files are genuinely solution-wide in effect.

**No row adds dependents**, and this looks like an omission so it is worth knowing why: whole-assembly
widening puts every method of the assembly into the changed set, and reverse reachability then
walks *backwards*, so everything downstream is already reached. Adding the project graph's
dependents would be a second, redundant expansion. Ticket 17's recompilation widening is the
exception, and only because inlining can *erase* the reference the reverse walk would have
followed — a different problem with a different mechanism.

**Row 6 is a named under-selection and it is deliberate.** The only rule in Reach that errs toward
selecting nothing, taken as an owner decision. The alternative is whole-solution selection for any
unrecognised file, which means **a README change runs the entire suite** — not conservatism with a
cost, but a tool nobody keeps switched on, firing on a large fraction of real pull requests. Rows
1–5 enumerate every build-affecting file that lives at a repository root, so the residue genuinely
is documentation-shaped: `README.md`, `.gitignore`, CI YAML, licence files, editor settings.

**Fully detectable**: `unmapped-file-no-project` fires when row 6 matched with **no ancestor
project**, so the notice is precise rather than a blanket disclaimer. Upgrade path is a configurable
pattern list, deliberately **not in M1** because there is no evidence yet about what real
repositories keep at their roots — do not add one.

**Generator detection is conventional, and says so when it fails.** `OutputItemType="Analyzer"` is
a convention rather than a prescription — the Roslyn cookbooks contain zero occurrences of it.
Reach checks the conventional markers: a `ProjectReference` carrying `OutputItemType="Analyzer"`,
and a project setting `IsRoslynComponent` or `EnforceExtendedAnalyzerRules`.

**When they do not match, do not fall back to whole-suite selection.** A generator arriving as a
`PackageReference` or a bare `<Analyzer>` item is invisible, and the honest answer is a register
entry, not a widening that fires hardest on solutions with **no generator at all**, which is most
of them. Errs under, undetectable — the failure is not knowing the generator exists.

**A generator edge is not a call edge.** It is a build-order and compilation-input relation, so it
does **not** enter the call graph and gets no edge provenance; it is consulted by this table at the
point a change is routed. This keeps the seven edge kinds intact rather than growing an eighth that
means something categorically different.

**Changes outside the analysis scope** are routed nowhere and reported with a category — *outside
any project*, or *inside a project no test project's closure reaches*. Whole-suite selection on an
unattributed path would fire on a docs-only PR, which is the exact change Reach exists to shrink.

## Acceptance criteria

- Routing is per change: a commit deleting one method and editing another in the same file
  produces **both** a whole-type widening and the edit's member root.
- Tier 2 fires for a changed file with no changed member, selecting every assembly whose symbols
  list the document — including a linked file compiled into two projects.
- A row per table row, asserting exactly which assembly instances widen.
- **A `Directory.Build.props` under a subdirectory does not select projects outside it.** Named
  negative test — it is the assertion that proves directory containment is real rather than
  aspirational.
- `global.json` widens every in-scope project; a `.csproj` widens only its own instances.
- **Row 6 with no ancestor project selects nothing and emits `unmapped-file-no-project`** — a
  `README.md` at the repository root is the canonical case.
- Row 6 *with* an ancestor project selects that project.
- A generator project detected by each of the three markers fires row 4; one arriving as a
  `PackageReference` fires nothing and emits no whole-suite widening.
- No generator relation appears in the call graph's edge set.
- A changed file in a project no test closure reaches is reported with its category and selects
  nothing.

## Out of scope

Recompilation widening — ticket 17. A configurable pattern list for row 6 — no evidence yet.
