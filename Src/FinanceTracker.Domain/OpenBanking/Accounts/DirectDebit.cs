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

    internal DirectDebit(DirectDebitDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.ExternalId);
        ExternalId = details.ExternalId;
        Update(details);
    }

    public string ExternalId { get; private set; }

    public string Name { get; private set; }

    public string Status { get; private set; }

    public DateTime PreviousPaymentDate { get; private set; }

    public decimal PreviousPaymentAmount { get; private set; }

    public string? Currency { get; private set; }

    public DateTime Timestamp { get; private set; }

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
