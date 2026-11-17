using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Direct debit mandate on a <see cref="BankAccount"/>.
/// </summary>
public sealed class DirectDebit : Entity
{
    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private DirectDebit()
    {
    }
#pragma warning restore CS8618

    /// <summary>
    /// A direct debit new from a sync.
    /// </summary>
    /// <param name="details">What the provider reports for it.</param>
    internal DirectDebit(DirectDebitDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.ExternalId);
        ExternalId = details.ExternalId;
        Update(details);
    }

    /// <summary>
    /// The provider's id for it.
    /// </summary>
    public string ExternalId { get; private set; }

    /// <summary>
    /// Who it pays.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// The provider's status for it, e.g. active.
    /// </summary>
    public string Status { get; private set; }

    /// <summary>
    /// When it last paid.
    /// </summary>
    public DateTime PreviousPaymentDate { get; private set; }

    /// <summary>
    /// What it last paid.
    /// </summary>
    public decimal PreviousPaymentAmount { get; private set; }

    /// <summary>
    /// The currency code of its payments, null when the provider doesn't say.
    /// </summary>
    public string? Currency { get; private set; }

    /// <summary>
    /// When the provider reported it.
    /// </summary>
    public DateTime Timestamp { get; private set; }

    /// <summary>
    /// Takes in what a later sync reports for it.
    /// </summary>
    /// <param name="details">What the provider reports for it now.</param>
    [MemberNotNull(nameof(Name), nameof(Status))]
    internal void Update(DirectDebitDetails details)
    {
        Name = details.Name;
        Status = details.Status;
        PreviousPaymentDate = details.PreviousPaymentDate;
        PreviousPaymentAmount = details.PreviousPaymentAmount;
        Currency = details.Currency;
        Timestamp = details.Timestamp;
    }
}
