# The unmappable-change rule table

Type: grilling
Status: open
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
