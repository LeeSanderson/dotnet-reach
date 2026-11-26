namespace Reach.Tests.Acceptance.Ledger;

/// <summary>
/// The acceptance tests over the Ledger sample, <c>tests/samples/Ledger/</c>. Every row here is
/// about what is <strong>not</strong> selected: Billing and Dunning are siblings, and
/// <c>Ledger.Api.Tests</c> has one test reaching each and one reaching neither. The two
/// <c>Known_gap_</c> tests pin what it selects that it should not.
/// </summary>
public sealed class LedgerTests() : SampleTests("Ledger")
{
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

    /// <summary>
    /// Rows 18 and 19: every Api test, the health test included, because recompilation widening
    /// widens the test project itself rather than walking back from what changed.
    /// </summary>
    private static readonly string[] AllOfApi =
    [
        "Ledger.Api.Tests.HealthTests.Health_check_reports_ok",
        "Ledger.Api.Tests.PostingTests.Posting_an_invoice_returns_its_total",
        "Ledger.Api.Tests.RemindersTests.Requesting_reminders_lists_the_overdue_invoices",
    ];

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
            Notices = EveryRunNotices,
        });

    [Fact]
    public async Task A_changed_const_widens_only_the_projects_that_reference_its_assembly() =>
        await Apply("standard-rate-const-change.patch").ExpectAsync(new()
        {
            Because = "TaxRules.StandardRatePercent is a const, which the compiler bakes into every "
                + "consumer, so every project referencing Billing widens whole: Ledger.Api.Tests "
                + "references it through Api, so all of it is selected, health test included, while "
                + "Domain and Dunning reference nothing of Billing's and are left out. Api holds no "
                + "IReminderChannel implementer, so widening it whole does not fan back into Dunning.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Ledger.Billing.Tests"] =
                [
                    "Ledger.Billing.Tests.InvoicePosterTests.Posted_invoices_are_numbered_in_sequence",
                    "Ledger.Billing.Tests.InvoicePosterTests.Posting_adds_tax_to_the_total",
                    "Ledger.Billing.Tests.TaxRulesTests.Tax_is_the_standard_rate_of_the_net",
                ],
                ["Ledger.Api.Tests"] = AllOfApi,
            },
            RunInFull = [],
            Notices = [.. EveryRunNotices, "recompilation-widening"],
            // The const is two entries: as a field it cannot join, so it falls back to Billing's
            // whole assembly; as a changed constant it widens Billing's referencers.
            Tiers = new()
            {
                ["Ledger.Billing.TaxRules.StandardRatePercent"] = Entries.Of(Tier.WholeAssembly, Tier.WholeAssembly),
            },
        });

    [Fact]
    public async Task A_removed_member_widens_only_the_projects_that_reference_its_assembly() =>
        await Apply("unused-overload-removed.patch").ExpectAsync(new()
        {
            Because = "Removing ReminderSchedule.Next(DateOnly) can rebind an untouched call in any "
                + "consumer, so every project referencing Dunning widens whole: Ledger.Api.Tests "
                + "references it through Api, so all of it is selected, health test included, while "
                + "Domain and Billing reference nothing of Dunning's and are left out.",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Ledger.Dunning.Tests"] =
                [
                    "Ledger.Dunning.Tests.OverdueInvoicesTests.Only_overdue_invoices_are_listed",
                    "Ledger.Dunning.Tests.ReminderScheduleTests.Each_reminder_waits_a_week_longer_than_the_last",
                    "Ledger.Dunning.Tests.ReminderSenderTests.A_reminder_names_the_days_overdue",
                ],
                ["Ledger.Api.Tests"] = AllOfApi,
            },
            RunInFull = [],
            Notices = [.. EveryRunNotices, "recompilation-widening"],
            // The deletion widens its type, and the removed member, as a removal, widens Dunning's
            // referencers. Dunning itself does not widen whole.
            Tiers = new()
            {
                ["Ledger.Dunning.ReminderSchedule"] = Tier.WholeType,
                ["Ledger.Dunning.ReminderSchedule.Next(DateOnly)"] = Tier.WholeAssembly,
            },
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
            Notices = EveryRunNotices,
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
            Notices = EveryRunNotices,
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
            Notices = EveryRunNotices,
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
            Notices = [.. EveryRunNotices, "unmapped-file-no-project"],
        });

    [Fact]
    [KnownGap]
    public async Task Known_gap_a_change_to_one_channel_selects_the_callers_of_every_channel() =>
        await Apply("email-channel-send-change.patch").ExpectAsync(new()
        {
            Because = "Only EmailChannel.Send changed, but ReminderSender calls through IReminderChannel, "
                + "so Api's reminders test, which only ever sends by webhook, is selected too: "
                + "docs/limitations.md, \"Widening residues from receiver-type inference\".",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Ledger.Dunning.Tests"] = ["Ledger.Dunning.Tests.ReminderSenderTests.A_reminder_names_the_days_overdue"],
                ["Ledger.Api.Tests"] = ["Ledger.Api.Tests.RemindersTests.Requesting_reminders_lists_the_overdue_invoices"],
            },
            RunInFull = [],
        });

    [Fact]
    [KnownGap]
    public async Task Known_gap_a_changed_const_in_the_core_assembly_selects_every_test() =>
        await Apply("default-currency-const-change.patch").ExpectAsync(new()
        {
            Because = "Only Money.DefaultCurrency changed, but it is a const in Domain, which every other "
                + "project references, so all twelve tests are selected and Changes names the one const "
                + "that did it: docs/limitations.md, \"Recompilation widening's blast radius\".",
            Outcome = Outcome.Selected,
            Selected = new()
            {
                ["Ledger.Domain.Tests"] =
                [
                    "Ledger.Domain.Tests.InvoiceTests.Overdue_counts_calendar_days",
                    "Ledger.Domain.Tests.InvoiceTests.The_total_sums_the_lines",
                    "Ledger.Domain.Tests.MoneyTests.Adding_amounts_in_different_currencies_is_refused",
                ],
                ["Ledger.Billing.Tests"] =
                [
                    "Ledger.Billing.Tests.InvoicePosterTests.Posted_invoices_are_numbered_in_sequence",
                    "Ledger.Billing.Tests.InvoicePosterTests.Posting_adds_tax_to_the_total",
                    "Ledger.Billing.Tests.TaxRulesTests.Tax_is_the_standard_rate_of_the_net",
                ],
                ["Ledger.Dunning.Tests"] =
                [
                    "Ledger.Dunning.Tests.OverdueInvoicesTests.Only_overdue_invoices_are_listed",
                    "Ledger.Dunning.Tests.ReminderScheduleTests.Each_reminder_waits_a_week_longer_than_the_last",
                    "Ledger.Dunning.Tests.ReminderSenderTests.A_reminder_names_the_days_overdue",
                ],
                ["Ledger.Api.Tests"] = AllOfApi,
            },
            RunInFull = [],
            // Two entries for the one const: Domain's whole assembly, which the field falls back to
            // and which two tests never reach (the health check and the reminder schedule), then
            // the widening of every project referencing Domain, which reaches all twelve.
            Changes = new() { ["Ledger.Domain.Money.DefaultCurrency"] = Entries.Of(10, 12) },
        });
}
