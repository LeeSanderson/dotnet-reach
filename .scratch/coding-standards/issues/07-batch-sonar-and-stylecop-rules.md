# 07: Batch: Sonar and StyleCop rules

**What to build:** Reach's own projects raise no SonarAnalyzer (S) or StyleCop (SA) diagnostics under the coding standard. That also covers any remaining long-tail rule from ticket 04's list that tickets 05 and 06 don't own. See [the spec](../spec.md).

This is a batch on the integration branch. The build stays red until ticket 09.

- S109 (magic numbers): name the constant. Many of Reach's numbers are documented limits (e.g. the command-line ceilings in ADR-0009), and a named constant should point at their reasoning.
- S1541 / S3776 / S1067 (complexity): refactor without changing behaviour. Where a split would hurt readability, stop and ask rather than suppress.
- SA1649 (file name must match the first type), plus the other S and SA rules in the list.
- Fix in code, one commit per rule. **No suppression without the maintainer's explicit approval for that rule.** Record any request under `## Comments`.

**Blocked by:** 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [x] Zero S and SA diagnostics in Reach's own projects (zero SA; the refactors below clear questions 3–6, and S4040/S4022 from questions 1–2 are fixed on their own branch)
- [x] No diagnostics left from ticket 04's list that no batch owns
- [x] No suppressions added unless approved (none added)
- [x] Reach's tests still pass with warnings not treated as errors (520 of 521; the one failure is the known CRLF acceptance test that ticket 08 owns)

## Comments

### Done

Branch `cs/07-s-sa` has one commit per rule. Where two rules share a file, they share a commit, and the commit message says so.
Fixed in full: IDE1006 (85), IDE0007, SA1649, SA1143, S109, S3267, S3059, S4261, S4017, S6605,
S6602, S6603, S3358, S3220, S131, S9415, S2325, S6354, S4136, S3872, S3241, S3240, S2699, S2365,
S2221, S127, S1192, S1172, S1227, S126, S1301, S1067. Partly fixed: S1541, S3776, S107, S4040.

Notes for review:

- **IDE1006**: renamed with the Roslyn renamer (a throwaway tool, not committed), so every reference follows.
  Nine local `const`s also had to become camelCase.
- **SA1649**: when a companion record came first, it moves below the type the file is named after.
  The enum extension classes (`Outcomes`, `Verbosities`, `EdgeProvenances`) and `ParseHealthCheck`
  move into their own files. `Measurement.cs` is renamed `ReportMeasurement.cs` and `Selection.cs`
  is renamed `SelectionResult.cs`.
- **S3059**: the report records' properties are now `internal` with `[JsonInclude]`. The serialised
  report is unchanged, and the report tests pass. The three `ICustomAttributeTypeProvider`-shaped
  methods on `SignatureNames` were public and unused, so they are removed.
- **S4017**: the four copies of `Bucket` are now one shared `Buckets.Bucket`.
- **S109**: the command-line ceilings now point at ADR-0009. The IL decoders use `sizeof(...)` and
  `byte.MaxValue`. Stack-slot counts now come from the `StackBehaviour` names, and branch opcodes
  from `OpCodes`. Both were checked against the old typed-out tables: equal for all 29 behaviours
  and all 65,536 opcode values.
- **S1227**: an IL instruction that cannot be decoded now reads as a `nop` with a negative next
  offset. The walks stop on their loop condition instead of `break`, and decode the same as before.
- **For 06**: the `Branches` table in `ReceiverTypes` adds one `(ILOpCode)` cast, so MA0181 is
  51, not 50. The S6966 sites (5) are exactly the CA1849 sites, so this batch leaves them to 06.
  Fixing CA1849 with the async overloads clears both rules.

### Questions for the maintainer (rules stopped; no suppressions added)

1. **S4040 at `TargetFrameworkMoniker.cs:30` and `:86`.** The platform is lower-cased, and the
   record's own doc gives that as its canonical form. Every comparison is already
   `OrdinalIgnoreCase`, so the lower-casing only shows in one place: the assembly-discovery notice
   ("built for .NETCoreApp,Version=v10.0 (windows)"). Without it, that notice would print the
   stamped casing (`Windows`). Is that output change acceptable, or should these two sites be
   suppressed?
2. **S4022 at `EdgeProvenance : byte`.** The enum backs the per-edge `EdgeProvenance[]` in
   `CallGraph`, so `Int32` storage makes that array four times larger. Should it change to `int`, or
   should the `byte` be kept with a suppression?
3. **S1541 / S3776 / S138 / S107 at `ReceiverTypes.Apply`.** This is one opcode switch with about
   25 case labels. A context object would fix S107 and S138. S1541 stays over 10 unless dispatch
   becomes a table of delegates, which reads worse than the switch. Should it be split or suppressed?
4. **S138 at `SourceRevision.Describe`** (94 lines). This is a flat switch with one case per member
   kind. Splitting it into two switches by member category would satisfy the rule but read worse.
   Should it be split or suppressed?
5. **S1541 / S138 at `ReachPipeline.SelectAsync`** (123 lines, complexity 12). This is the linear
   pipeline with an early return per phase. Splitting it at the build step into two methods would
   pass about 9 locals, so S107 would fire instead. Should it be split (and how), or suppressed?
6. **S1200 (class coupling over 30)** on `SourceRevision` (42, mostly Roslyn syntax types),
   `CallGraphBuilder` (42), `ReachPipeline` (41), `ChangedSetBuilder` (33) and `Selector` (33). Each
   fix means splitting a class, which is a design decision. The S4017 fix added nested index types
   to `ChangedSetBuilder` and `Selector`, which raised them from 32 and 31. **S107 at
   `Selector.Select`** (8 parameters) depends on how `Selector` is split. Should each of these be
   split, and along what lines, or suppressed?

### Maintainer decisions on questions 3–6

> **Flat switches** (`ReceiverTypes.Apply`, `SourceRevision.Describe`): "Split by family" — group
> cases into small methods by family (e.g. loads/stores/calls; types/methods/properties); the
> switch dispatches to them; each method under the limits.

> **Big orchestrators/classes** (`SelectAsync`, the S1200 classes, `Selector.Select` S107):
> "Refactor properly" — introduce state/request objects (e.g. a pipeline context) and split each
> class along its real responsibilities until the rules pass. Behaviour-preserving.

No suppressions.

### Done: questions 3–6 (branch `cs/07c-refactors`, one commit per class)

- **`ReceiverTypes`**: the interpretation keeps its stack, arguments, locals and inferences as
  state. A table classifies each opcode into a family (construction, conversion, argument load,
  local load, field load, duplication, call, function load), and `Apply` switches on the family
  to one small method each. IL decoding (instructions, branch targets, stack effects) moves to
  `ILDecoder`; what a metadata token names, loads or returns moves to `OperandTypes`.
- **`SourceRevision`**: reading one member moves to `MemberDeclarations`, whose switch dispatches
  on Roslyn's own families (`BaseMethodDeclarationSyntax`, `BasePropertyDeclarationSyntax`,
  `BaseFieldDeclarationSyntax`, plus delegates and enum members). `SourceRevision` keeps the types.
- **`ChangedSetBuilder`**: comparing the two sides' declared types moves to
  `DeclarationComparison`. Collecting a revision's types becomes `DeclarationsByTypeName.Collect`.
- **`Selector`**: test enumeration moves to `TestEnumeration`. The reverse walk and its per-test
  accumulators move to `ReachedTests`. `Select` takes the `CallGraphResult` in place of its
  graph and assemblies (always the same build's), which clears S107.
- **`CallGraphBuilder`**: token resolution and its notices move to `CallSiteResolver`. Dispatch
  anchoring and widening move to `DispatchAnchors`, which owns the one type hierarchy.
- **`ReachPipeline`**: the run's shared state becomes `PipelineRun`. Baseline, changed set and
  build move to `ChangePhases`. Discovery through rendering moves to `SelectionPhases`.
  `ReachPipeline` keeps the usage checks and the sequencing.

Reach.Tests: 521 of 521 pass, `ExampleReportTests` included, run against this worktree's own
repository root.
