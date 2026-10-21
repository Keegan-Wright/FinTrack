using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Standing order on a <see cref="BankAccount"/>. Identified by
/// reference and payee.
/// </summary>
public sealed class StandingOrder : Entity
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private StandingOrder()
    {
    }
#pragma warning restore CS8618

    internal StandingOrder(StandingOrderDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Reference = details.Reference;
        Payee = details.Payee;
        Update(details);
    }

    public string Reference { get; private set; }

    public string Payee { get; private set; }

    public string Frequency { get; private set; }

    public string Status { get; private set; }

    public string Currency { get; private set; }

    public DateTime NextPaymentDate { get; private set; }

    public decimal NextPaymentAmount { get; private set; }

    public DateTime FirstPaymentDate { get; private set; }

    public decimal FirstPaymentAmount { get; private set; }

    public DateTime FinalPaymentDate { get; private set; }

    public decimal FinalPaymentAmount { get; private set; }

    public DateTime Timestamp { get; private set; }

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
