using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.Utility;

namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// Direct debit mandate on a <see cref="BankAccount"/>.
/// </summary>
public sealed class DirectDebit : Entity
{
    [Obsolete("EF Use Only")]
    private DirectDebit()
    {
    }

    internal DirectDebit(DirectDebitDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        ArgumentException.ThrowIfNullOrWhiteSpace(details.ExternalId);
        ExternalId = details.ExternalId;
        Update(details);
    }

    [Encrypt] public string ExternalId { get; private set; }

    [Encrypt] public string Name { get; private set; }

    [Encrypt] public string Status { get; private set; }

    [Encrypt] public DateTime PreviousPaymentDate { get; private set; }

    [Encrypt] public decimal PreviousPaymentAmount { get; private set; }

    [Encrypt] public string? Currency { get; private set; }

    [Encrypt] public DateTime Timestamp { get; private set; }

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
