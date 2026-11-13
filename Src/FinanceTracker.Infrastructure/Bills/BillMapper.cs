using FinanceTracker.Contracts.Bills;
using FinanceTracker.Domain.Bills;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Transactions;
using Bill = FinanceTracker.Contracts.Bills.Bill;
using BillFrequency = FinanceTracker.Contracts.Bills.BillFrequency;
using BillPeriod = FinanceTracker.Contracts.Bills.BillPeriod;
using BillSource = FinanceTracker.Contracts.Bills.BillSource;
using BillState = FinanceTracker.Contracts.Bills.BillState;
using BillSuggestion = FinanceTracker.Contracts.Bills.BillSuggestion;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillFrequency = FinanceTracker.Domain.Bills.BillFrequency;
using DomainBillPeriod = FinanceTracker.Domain.Bills.BillPeriod;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;
using DomainBillState = FinanceTracker.Domain.Bills.BillState;
using DomainBillSuggestion = FinanceTracker.Domain.Bills.BillSuggestion;

namespace FinanceTracker.Infrastructure.Bills;

/// <summary>
/// Between the domain's bills and their contracts. The enums share names, so they map by name.
/// </summary>
internal static class BillMapper
{
    /// <param name="payments">The transactions paying the bill, for its latest payment.</param>
    /// <param name="today">Today where the user is, for the next due date and monthly cost.</param>
    /// <param name="sharedBy">Whoever shares it with the user, null when it's theirs.</param>
    /// <param name="canChange">Whether the user can edit it.</param>
    public static Bill ToContract(DomainBill bill, string accountName, IEnumerable<BankTransaction> payments,
        TimeZoneInfo timeZone, DateOnly today, string? sharedBy = null, bool canChange = true)
    {
        BankTransaction? latest = payments
            .Where(x => x.PaysBill(bill.Id))
            .MaxBy(static x => x.OccurredAt);

        return new Bill
        {
            Id = bill.Id,
            Name = bill.Name,
            Payee = bill.Payee,
            AccountId = bill.AccountId,
            AccountName = accountName,
            Source = Map<DomainBillSource, BillSource>(bill.Source),
            Frequency = Map<DomainBillFrequency, BillFrequency>(bill.Frequency),
            StartsOn = bill.StartsOn,
            NextDue = bill.NextDue(today),
            EndedOn = bill.EndedOn,
            EndedByUser = bill.EndedByUser,
            ExpectedAmount = bill.ExpectedAmount,
            TolerancePercent = bill.TolerancePercent,
            MonthlyCost = MonthlyCost(bill, today),
            LastPaid = latest is null ? null : -latest.Amount,
            LastPaidOn = latest is null ? null : latest.OccurredAt.LocalDayIn(timeZone),
            FollowsTheBank = bill.Source == DomainBillSource.StandingOrder,
            CanDelete = bill.SourceId is null && sharedBy is null,
            SharedBy = sharedBy,
            CanChange = canChange
        };
    }

    public static BillPeriod ToContract(DomainBillPeriod period) => new()
    {
        Due = period.Due,
        Expected = period.Expected,
        State = Map<DomainBillState, BillState>(period.State),
        Paid = period.Paid,
        PaidOn = period.PaidOn,
        PaymentIds = period.PaymentIds.ToList()
    };

    /// <param name="payments">The transactions behind it, those not found, e.g. since deleted, are left out.</param>
    public static BillSuggestion ToContract(DomainBillSuggestion suggestion, string accountName,
        IReadOnlyDictionary<Guid, BankTransaction> payments, DateOnly today) => new()
    {
        Id = suggestion.Id,
        Name = suggestion.Name,
        AccountId = suggestion.AccountId,
        AccountName = accountName,
        Frequency = Map<DomainBillFrequency, BillFrequency>(suggestion.Frequency),
        Amount = suggestion.Amount,
        NextDue = suggestion.ToBill().NextDue(today),
        Payments = suggestion.PaymentIds
            .Where(payments.ContainsKey)
            .Select(x => payments[x])
            .Select(static x => new SuggestedPayment
            {
                TransactionId = x.Id, Description = x.Description, Amount = x.Amount, OccurredAt = x.OccurredAt
            })
            .ToList()
    };

    public static DomainBillFrequency ToDomain(BillFrequency frequency) =>
        Map<BillFrequency, DomainBillFrequency>(frequency);

    /// <summary>
    /// Nothing from the day it ends, as nothing more is committed.
    /// </summary>
    private static decimal MonthlyCost(DomainBill bill, DateOnly today)
    {
        if (bill.EndedOn <= today)
        {
            return 0;
        }

        return BillCalculator.MonthlyCost(bill, today);
    }

    private static TTo Map<TFrom, TTo>(TFrom value) where TFrom : struct, Enum where TTo : struct, Enum =>
        Enum.Parse<TTo>(value.ToString());
}
