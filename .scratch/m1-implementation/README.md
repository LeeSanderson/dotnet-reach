# M1 implementation

The build effort for Reach's walking skeleton. Planning is done; this directory holds only
work that produces code.

**The specification is [`.scratch/walking-skeleton/spec.md`](../walking-skeleton/spec.md).**
Every ticket here cites the section it implements. Where a ticket and the spec disagree, the
spec wins and the ticket has a bug; where the spec and an ADR disagree, the ADR wins and the
spec has a bug.

Do **not** treat [`.scratch/walking-skeleton/`](../walking-skeleton/) as a backlog. Its
`issues/` directory holds the twenty-two *decision* tickets that produced the spec, all
resolved, and its `map.md` is the wayfinder map. Those are the record of how each rule was
argued, not work to do. Zoom one when a ticket here says to.

## Tickets

Numbered in the order they land. Each is sized to one agent session.

**Stage A — the spine.** At the end of it, `dotnet reach select` runs end to end: a change
selects tests over **compiled edges only**, and emits `report.json` plus runnable argument
vectors.

| # | Ticket |
|---|---|
| 01 | [Repository scaffolding and the CI gate](issues/01-repository-scaffolding-and-ci.md) |
| 02 | [The process port and the git adapter](issues/02-process-port-and-git-adapter.md) |
| 03 | [Baseline resolution](issues/03-baseline-resolution.md) |
| 04 | [Target discovery, solution parsing and analysis scope](issues/04-target-discovery-and-analysis-scope.md) |
| 05 | [The CLI surface](issues/05-the-cli-surface.md) |
| 06 | [The changed set](issues/06-the-changed-set.md) |
| 07 | [Build invocation and assembly discovery](issues/07-build-and-assembly-discovery.md) |
| 08 | [Source–binary correspondence verification](issues/08-source-binary-correspondence.md) |
| 09 | [The call graph: identity, the metadata pass and compiled edges](issues/09-the-call-graph.md) |
| 10 | [The join](issues/10-the-join.md) |
| 11 | [Test recognition, the reverse walk and selection](issues/11-test-recognition-and-the-reverse-walk.md) |
| 12 | [The report writer, exit codes and the human summary](issues/12-the-report-writer.md) |
| 13 | [Rendering and delivery](issues/13-rendering-and-delivery.md) |

**Stage B — correctness.** Stage A alone under-selects enormously. Nothing is published
until stage C, and the gate is the whole set.

| # | Ticket |
|---|---|
| 14 | [Widening: the type hierarchy index and receiver-type inference](issues/14-widening.md) |
| 15 | [Synthesised edges: containment and type initialization](issues/15-synthesised-edges.md) |
| 16 | [The tier ladder and the unmappable-change rule table](issues/16-tier-ladder-and-rule-table.md) |
| 17 | [Recompilation widening and the parse-failure rules](issues/17-recompilation-widening-and-parse-rules.md) |
| 18 | [The notice catalogue, the register parity test and the measurement](issues/18-notices-register-and-measurement.md) |

**Stage C — acceptance and release.**

| # | Ticket |
|---|---|
| 19 | [The fixture solution and the acceptance assertions](issues/19-fixture-solution-and-acceptance.md) |
| 20 | [The documentation set and the committed example report](issues/20-documentation-set.md) |
| 21 | [Publishing](issues/21-publishing.md) |
| 22 | [dogfood.yml and the docs↔workflow parity test](issues/22-dogfood-and-parity-test.md) |

## Two hard ordering constraints

Both inherited from the design rather than invented here.

- **`ci.yml` is buildable from the first commit** — ticket 01.
- **`dogfood.yml`, the docs↔workflow parity test and the fenced YAML block in
  `docs/adopting-reach.md` are one unit of work that arrives with `0.1.0-alpha.1` and cannot
  be written earlier.** The documented recipe is a `pull_request` workflow installing a
  *published* package. You cannot dogfood before you publish, and you do not need to.

## Standing rules for every ticket

1. **Under-selection is a correctness bug.** Where a choice is genuinely uncertain, take the
   option that widens. Spec §1.1.
2. **A simplification that under-selects owes a register entry** in
   [`docs/limitations.md`](../../docs/limitations.md), and a notice code wherever Reach can
   detect an instance. Spec §1.2, §1.3.
3. **Anything testable in memory is tested in memory.** Spec §16.2.
4. **Do not add an interface for a seam with one implementation.** Spec §16.1 — Reach will be
   pointed at its own repository, and gratuitous indirection is what makes widening fan out.
5. **Use [CONTEXT.md](../../CONTEXT.md)'s vocabulary** in identifiers and in prose, and avoid
   the alternatives it lists.
