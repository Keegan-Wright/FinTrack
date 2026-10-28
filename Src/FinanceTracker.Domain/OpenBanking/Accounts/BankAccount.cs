using System.Diagnostics.CodeAnalysis;
using FinanceTracker.Domain.Common;
using FinanceTracker.Domain.OpenBanking.Balances;

namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// An account exposed by a <see cref="Connections.BankConnection"/>.
/// Owns its balance, standing orders and direct debits. Transactions and the sync log reference the account.
/// </summary>
public sealed class BankAccount : AggregateRoot
{
    private readonly List<StandingOrder> _standingOrders = [];
    private readonly List<DirectDebit> _directDebits = [];

    [Obsolete("EF Use Only")]
#pragma warning disable CS8618 // EF Core sets the properties when it materialises the entity
    private BankAccount()
    {
    }
#pragma warning restore CS8618

    public BankAccount(Guid userId, Guid connectionId, string externalId, string accountType, string displayName,
        string currency) : base(userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(connectionId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        ConnectionId = connectionId;
        ExternalId = externalId;
        UpdateDetails(accountType, displayName, currency);
    }

    public Guid ConnectionId { get; private set; }

    public string ExternalId { get; private set; }

    public string AccountType { get; private set; }

    public string DisplayName { get; private set; }

    public string Currency { get; private set; }

    public AccountBalance? Balance { get; private set; }

    /// <summary>
    /// The account's sort code and number or IBAN, null when the provider gives neither.
    /// </summary>
    public AccountIdentifier? Identifier { get; private set; }

    public IReadOnlyCollection<StandingOrder> StandingOrders => _standingOrders.AsReadOnly();

    public IReadOnlyCollection<DirectDebit> DirectDebits => _directDebits.AsReadOnly();

    [MemberNotNull(nameof(AccountType), nameof(DisplayName), nameof(Currency))]
    public void UpdateDetails(string accountType, string displayName, string currency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountType);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);

        AccountType = accountType;
        DisplayName = displayName;
        Currency = currency;
    }

    public void UpdateIdentifier(AccountIdentifier? identifier) => Identifier = identifier;

    public void UpdateBalance(decimal available, decimal current, DateTime asOf) =>
        Balance = new AccountBalance(available, current, asOf);

    /// <summary>
    /// Replaces the standing orders with the provider's current view.
    /// </summary>
    public void SyncStandingOrders(IReadOnlyCollection<StandingOrderDetails> standingOrders)
    {
        ArgumentNullException.ThrowIfNull(standingOrders);

        _standingOrders.RemoveAll(existing => !standingOrders.Any(existing.Matches));

        foreach (StandingOrderDetails details in standingOrders)
        {
            StandingOrder? existing = _standingOrders.FirstOrDefault(x => x.Matches(details));
            if (existing is null)
            {
                _standingOrders.Add(new StandingOrder(details));
            }
            else
            {
                existing.Update(details);
            }
        }
    }

    /// <summary>
    /// Replaces the direct debits with the provider's current view.
    /// </summary>
    public void SyncDirectDebits(IReadOnlyCollection<DirectDebitDetails> directDebits)
    {
        ArgumentNullException.ThrowIfNull(directDebits);

        _directDebits.RemoveAll(existing => directDebits.All(x => x.ExternalId != existing.ExternalId));

        foreach (DirectDebitDetails details in directDebits)
        {
            DirectDebit? existing = _directDebits.FirstOrDefault(x => x.ExternalId == details.ExternalId);
            if (existing is null)
            {
                _directDebits.Add(new DirectDebit(details));
            }
            else
            {
                existing.Update(details);
            }
        }
    }

    /// <summary>
    /// Creates a log entry for a completed sync.
    /// </summary>
    public SyncLogEntry RecordSync(SyncType type, DateTime occurredAt) => new(Id, type, occurredAt);

    /// <summary>
    /// Takes the account's first snapshot for <paramref name="day"/>, from its stored balance. Later syncs that day
    /// update it with <see cref="BalanceSnapshot.Refresh"/>.
    /// </summary>
    /// <param name="synced">
    /// Whether the balance synced this time. When it didn't, the snapshot holds the last stored balance, flagged as
    /// stale.
    /// </param>
    /// <exception cref="InvalidOperationException">The account has never had a balance.</exception>
    public BalanceSnapshot SnapshotBalance(DateOnly day, bool synced)
    {
        if (Balance is null)
        {
            throw new InvalidOperationException($"Account {Id} has no balance to snapshot.");
        }

        return new BalanceSnapshot(UserId, Id, day, Balance with { }, BalanceSnapshotSource.Synced, isStale: !synced);
    }
}
