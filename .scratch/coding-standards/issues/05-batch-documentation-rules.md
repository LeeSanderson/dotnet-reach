# 05: Batch: documentation rules

**What to build:** Reach's own projects raise no documentation diagnostics under the coding standard: MA0211 (multi-line XML summaries), CS1573 (missing `<param>` on partially documented members and records), and any other XML-doc rules in ticket 04's list. See [the spec](../spec.md).

This is a batch on the integration branch. The build stays red until ticket 09.

Fix in code, one commit per rule. The documentation should still read well: reshape summaries; don't just pad them. **No suppression without the maintainer's explicit approval for that rule.** If a rule looks wrong for this codebase, stop and ask, and record the question under `## Comments`.

**Blocked by:** 04 (add the package and apply the automatic fixes)

**Status:** ready-for-agent

- [x] Zero MA0211 and CS1573 diagnostics in Reach's own projects
- [x] Zero remaining documentation-family diagnostics from ticket 04's list
- [x] No suppressions added unless approved (approval quoted under `## Comments`)
- [x] Reach's tests still pass with warnings not treated as errors

## Comments

Documentation diagnostics in Reach's own projects at the start (rebuilt on `cs/05-docs`, matching
ticket 04's list): CS1573 106 (Core 92, Tests 14), CS1572 1, CS1734 1. MA0211 had none left in
Reach's own projects; ticket 04's auto-fix cleared them.

Two commits:

1. **CS1572, CS1734**: `JoinResult` documented `Joined`, which is a property, not a parameter. The
   text moves to the property's own `<summary>`, and the record documents `Member` and `Methods`.
   `ChildScript.Chatty()` used `<paramref name="lines"/>` for the line count, which is the script's
   first argument. The summary now says that.
2. **CS1573**: every partially documented record and method now documents all of its parameters
   with real text. Where a record had only `<param>` tags, I added a short `<summary>`
   (`DeclaredMember`, `RenderedEntry`, `ReportChange`, `ReportCounts`, `SelectedTest`, `TypeInfo`,
   `ExternalAnchors.Intern`, `Selector.RecordPaths`, the second `Compiled.Assembly` overload,
   `Selections.Of`). `SelectRun`'s summary now says that each phase's result is null when the run
   stopped before that phase, so the 12 phase params can stay short.

No suppressions.

**Not done here, by ownership:** MA0211 still fires in the Contoso fixtures
(`tests/fixtures/Contoso/src/Contoso.Core/Invoice.cs:5`,
`tests/fixtures/Contoso/tests/Contoso.Tests.Unrecognised/InvoiceChecks.cs:3`). The fixture projects
belong to ticket 08.

Tests: `dotnet test --project tests/Reach.Tests/Reach.Tests.csproj -p:TreatWarningsAsErrors=false
-p:RunAnalyzers=false` gives 520 passed, 1 failed. The failure is the known
`AcceptanceTests.Fact_added_to_an_existing_method_selects_it` CRLF regression, which belongs to 08.
