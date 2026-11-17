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

    /// <summary>
    /// A standing order new from a sync.
    /// </summary>
    /// <param name="details">What the provider reports for it.</param>
    internal StandingOrder(StandingOrderDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        Reference = details.Reference;
        Payee = details.Payee;
        Update(details);
    }

    /// <summary>
    /// The reference on its payments.
    /// </summary>
    public string Reference { get; private set; }

    /// <summary>
    /// Who it pays.
    /// </summary>
    public string Payee { get; private set; }

    /// <summary>
    /// How often it pays, as the provider describes it.
    /// </summary>
    public string Frequency { get; private set; }

    /// <summary>
    /// The provider's status for it, e.g. active.
    /// </summary>
    public string Status { get; private set; }

    /// <summary>
    /// The currency code of its payments.
    /// </summary>
    public string Currency { get; private set; }

    /// <summary>
    /// When it next pays.
    /// </summary>
    public DateTime NextPaymentDate { get; private set; }

    /// <summary>
    /// What it next pays.
    /// </summary>
    public decimal NextPaymentAmount { get; private set; }

    /// <summary>
    /// When it first paid.
    /// </summary>
    public DateTime FirstPaymentDate { get; private set; }

    /// <summary>
    /// What it first paid.
    /// </summary>
    public decimal FirstPaymentAmount { get; private set; }

    /// <summary>
    /// When it last pays.
    /// </summary>
    public DateTime FinalPaymentDate { get; private set; }

    /// <summary>
    /// What it last pays.
    /// </summary>
    public decimal FinalPaymentAmount { get; private set; }

    /// <summary>
    /// When the provider reported it.
    /// </summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>
    /// Whether <paramref name="details"/> are for this standing order, the same reference and payee.
    /// </summary>
    /// <param name="details">What the provider reports.</param>
    internal bool Matches(StandingOrderDetails details) =>
        Reference == details.Reference && Payee == details.Payee;

    /// <summary>
    /// Takes in what a later sync reports for it.
    /// </summary>
    /// <param name="details">What the provider reports for it now.</param>
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
