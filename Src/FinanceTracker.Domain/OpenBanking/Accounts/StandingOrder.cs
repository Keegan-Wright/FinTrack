using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Standing order on a <see cref="BankAccount"/>. Identified by
/// reference and payee.
/// </summary>
public sealed class StandingOrder : Entity
{
    [Obsolete("EF Use Only")]
    private StandingOrder()
    {
    }

    internal StandingOrder(StandingOrderDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Reference = details.Reference;
        Payee = details.Payee;
        Update(details);
    }

    [Encrypt] public string Reference { get; private set; }

    [Encrypt] public string Payee { get; private set; }

    [Encrypt] public string Frequency { get; private set; }

    [Encrypt] public string Status { get; private set; }

    [Encrypt] public string Currency { get; private set; }

    [Encrypt] public DateTime NextPaymentDate { get; private set; }

    [Encrypt] public decimal NextPaymentAmount { get; private set; }

    [Encrypt] public DateTime FirstPaymentDate { get; private set; }

    [Encrypt] public decimal FirstPaymentAmount { get; private set; }

    [Encrypt] public DateTime FinalPaymentDate { get; private set; }

    [Encrypt] public decimal FinalPaymentAmount { get; private set; }

    [Encrypt] public DateTime Timestamp { get; private set; }

    internal bool Matches(StandingOrderDetails details) =>
        Reference == details.Reference && Payee == details.Payee;

    [MemberNotNull(nameof(Frequency), nameof(Status), nameof(Currency))]
    internal void Update(StandingOrderDetails details)
    {
        Frequency = details.Frequency;
        Status = details.Status;
        Currency = details.Currency;
        NextPaymentDate = details.NextPaymentDate;
        NextPaymentAmount = details.NextPaymentAmount;
        FirstPaymentDate = details.FirstPaymentDate;
        FirstPaymentAmount = details.FirstPaymentAmount;
        FinalPaymentDate = details.FinalPaymentDate;
        FinalPaymentAmount = details.FinalPaymentAmount;
        Timestamp = details.Timestamp;
    }
}
