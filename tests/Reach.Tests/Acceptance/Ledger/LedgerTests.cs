namespace Reach.Tests.Acceptance.Ledger;

/// <summary>
/// The acceptance tests over the Ledger sample, <c>tests/samples/Ledger/</c>. Every row here is
/// about what is <strong>not</strong> selected: Billing and Dunning are siblings, and
/// <c>Ledger.Api.Tests</c> has one test reaching each and one reaching neither.
/// </summary>
public sealed class LedgerTests() : SampleTests("Ledger")
{
    /// <summary>
    /// The two notices every run carries: the harness passes the baseline as <c>--base</c>, and
    /// the change patches are left uncommitted, so it is also <c>HEAD</c>.
    /// </summary>
    private static readonly string[] StandingNotices = ["baseline-is-head", "baseline-resolved"];

    /// <summary>
    /// Rows 20 and 22: Billing widens as a whole. Every Billing test reaches Billing, and so
    /// does Api's posting test; nothing else does.
    /// </summary>
    private static readonly Dictionary<string, IReadOnlyList<string>> BillingWidenedAlone = new()
    {
        ["Ledger.Billing.Tests"] =
        [
            "Ledger.Billing.Tests.InvoicePosterTests.Posted_invoices_are_numbered_in_sequence",
            "Ledger.Billing.Tests.InvoicePosterTests.Posting_adds_tax_to_the_total",
            "Ledger.Billing.Tests.TaxRulesTests.Tax_is_the_standard_rate_of_the_net",
        ],
        ["Ledger.Api.Tests"] = ["Ledger.Api.Tests.PostingTests.Posting_an_invoice_returns_its_total"],
    };

    [Fact]
    public async Task A_domain_change_reached_only_through_dunning_leaves_billing_out() =>
        await Apply("days-overdue-body-change.patch").ExpectAsync(new()
        {
            Because = "Only Invoice.DaysOverdue's body changed, and only Dunning and one Domain test "
                + "call it: Billing's tests, the other Domain tests, the Dunning schedule test and "
                + "Api's posting and health tests never reach it.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Ledger.Domain.Tests"] = ["Ledger.Domain.Tests.InvoiceTests.Overdue_counts_calendar_days"],
                ["Ledger.Dunning.Tests"] =
                [
                    "Ledger.Dunning.Tests.OverdueInvoicesTests.Only_overdue_invoices_are_listed",
                    "Ledger.Dunning.Tests.ReminderSenderTests.A_reminder_names_the_days_overdue",
                ],
                ["Ledger.Api.Tests"] = ["Ledger.Api.Tests.RemindersTests.Requesting_reminders_lists_the_overdue_invoices"],
            },
            RunInFull = [],
            Notices = StandingNotices,
        });

    [Fact]
    public async Task A_project_file_change_widens_its_project_and_leaves_its_sibling_out() =>
        await Apply("billing-project-nowarn.patch").ExpectAsync(new()
        {
            Because = "Ledger.Billing.csproj changed, so Billing widens whole and the reverse walk "
                + "reaches Billing's tests and Api's posting test; no project file rule adds "
                + "dependents, so Domain, Dunning and Api's reminders and health tests are left out.",
            Outcome = Outcome.Selected,
            Selected = BillingWidenedAlone,
            RunInFull = [],
            Notices = StandingNotices,
        });

    [Fact]
    public async Task A_directory_build_props_change_widens_only_the_projects_beneath_it() =>
        await Apply("collections-build-props.patch").ExpectAsync(new()
        {
            Because = "src/Collections/Directory.Build.props covers Ledger.Dunning alone, so Dunning "
                + "widens and the reverse walk reaches Dunning's tests (outside the folder) and Api's "
                + "reminders test; Domain, Billing and Api's posting and health tests are left out.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Ledger.Dunning.Tests"] =
                [
                    "Ledger.Dunning.Tests.OverdueInvoicesTests.Only_overdue_invoices_are_listed",
                    "Ledger.Dunning.Tests.ReminderScheduleTests.Each_reminder_waits_a_week_longer_than_the_last",
                    "Ledger.Dunning.Tests.ReminderSenderTests.A_reminder_names_the_days_overdue",
                ],
                ["Ledger.Api.Tests"] = ["Ledger.Api.Tests.RemindersTests.Requesting_reminders_lists_the_overdue_invoices"],
            },
            RunInFull = [],
            Notices = StandingNotices,
        });

    [Fact]
    public async Task A_namespace_move_widens_the_whole_assembly_and_no_further() =>
        await Apply("invoice-numbering-namespace-move.patch").ExpectAsync(new()
        {
            Because = "InvoiceNumbering moved namespace, so its old name is gone and Billing widens "
                + "whole, selecting what a Billing project file change does; a deleted type is not a "
                + "removed member, so no recompilation widening reaches Api's other tests.",
            Outcome = Outcome.Selected,
            Selected = BillingWidenedAlone,
            RunInFull = [],
            Notices = StandingNotices,
            // The old name is a deleted type. Under the new name every member is added: Next() joins
            // to its compiled method, and the field, which has no method to join, falls back to
            // its whole assembly.
            Tiers = new()
            {
                ["Ledger.Billing.InvoiceNumbering"] = Tier.WholeAssembly,
                ["Ledger.Billing.Numbering.InvoiceNumbering._last"] = Tier.WholeAssembly,
                ["Ledger.Billing.Numbering.InvoiceNumbering.Next()"] = Tier.Member,
            },
        });

    [Fact]
    public async Task A_root_file_matching_no_rule_selects_nothing_and_says_so() =>
        await Apply("codeowners-added.patch").ExpectAsync(new()
        {
            Because = "CODEOWNERS matches no rule and sits in no project, so the one rule that errs "
                + "toward selecting nothing leaves every test out, and says so with a notice.",
            Outcome = Outcome.NothingSelected,
            Selected = new(),
            RunInFull = [],
            Notices = [.. StandingNotices, "unmapped-file-no-project"],
        });
}
