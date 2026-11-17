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

    /// <param name="userId">The user it belongs to.</param>
    /// <param name="connectionId">The bank connection it comes through.</param>
    /// <param name="externalId">The provider's id for it.</param>
    /// <param name="accountType">What the provider says it is, e.g. a current or savings account.</param>
    /// <param name="displayName">The name the provider gives it.</param>
    /// <param name="currency">Its currency code, e.g. GBP.</param>
    /// <param name="kind">Whether it's an account or a card.</param>
    public BankAccount(Guid userId, Guid connectionId, string externalId, string accountType, string displayName,
        string currency, BankAccountKind kind = BankAccountKind.Account) : base(userId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(connectionId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        ConnectionId = connectionId;
        ExternalId = externalId;
        Kind = kind;
        UpdateDetails(accountType, displayName, currency);
    }

    /// <summary>
    /// The bank connection it comes through.
    /// </summary>
    public Guid ConnectionId { get; private set; }

    /// <summary>
    /// The provider's id for it.
    /// </summary>
    public string ExternalId { get; private set; }

    /// <summary>
    /// Whether it's an account or a card.
    /// </summary>
    public BankAccountKind Kind { get; private set; }

    /// <summary>
    /// What the provider says it is, e.g. a current or savings account.
    /// </summary>
    public string AccountType { get; private set; }

    /// <summary>
    /// The name the provider gives it.
    /// </summary>
    public string DisplayName { get; private set; }

    /// <summary>
    /// Its currency code, e.g. GBP.
    /// </summary>
    public string Currency { get; private set; }

    /// <summary>
    /// Its latest balance, null until the first sync.
    /// </summary>
    public AccountBalance? Balance { get; private set; }

    /// <summary>
    /// The account's sort code and number or IBAN, <see cref="AccountIdentifier.Unknown"/> when the provider gives
    /// neither, see <see cref="AccountIdentifier.IsKnown"/>.
    /// </summary>
    public AccountIdentifier Identifier { get; private set; } = AccountIdentifier.Unknown();

    /// <summary>
    /// The card's network, number ending and so on, <see cref="CardDetails.None"/> for an account.
    /// </summary>
    public CardDetails Card { get; private set; } = CardDetails.None();

    /// <summary>
    /// Whether its past closing balances have been worked out, see <see cref="BalanceHistory"/>. Done once, after its
    /// first sync.
    /// </summary>
    public bool HistoryBackfilled { get; private set; }

    /// <summary>
    /// Its standing orders as of the last sync.
    /// </summary>
    public IReadOnlyCollection<StandingOrder> StandingOrders => _standingOrders.AsReadOnly();

    /// <summary>
    /// Its direct debits as of the last sync.
    /// </summary>
    public IReadOnlyCollection<DirectDebit> DirectDebits => _directDebits.AsReadOnly();

    /// <summary>
    /// Takes in the details the provider reports for it.
    /// </summary>
    /// <param name="accountType">What the provider says it is.</param>
    /// <param name="displayName">The name the provider gives it.</param>
    /// <param name="currency">Its currency code.</param>
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

    /// <param name="identifier">Null when the provider gives no sort code, number or IBAN.</param>
    public void UpdateIdentifier(AccountIdentifier? identifier) =>
        Identifier = identifier ?? AccountIdentifier.Unknown();

    /// <param name="card">The card's details.</param>
    /// <exception cref="InvalidOperationException">The account isn't a card.</exception>
    public void UpdateCard(CardDetails card)
    {
        ArgumentNullException.ThrowIfNull(card);
        if (Kind != BankAccountKind.Card)
        {
            throw new InvalidOperationException($"Account {Id} isn't a card.");
        }

        Card = card;
    }

    /// <summary>
    /// Sets its latest balance.
    /// </summary>
    /// <param name="available">What can be spent now, or for a card the credit left.</param>
    /// <param name="current">For a card, what's owed as a negative amount.</param>
    /// <param name="asOf">When the provider worked it out.</param>
    /// <param name="creditLimit">A card's credit limit, null for an account.</param>
    public void UpdateBalance(decimal available, decimal current, DateTime asOf, decimal? creditLimit = null) =>
        Balance = new AccountBalance(available, current, asOf, creditLimit);

    /// <summary>
    /// Replaces the standing orders with the provider's current view.
    /// </summary>
    /// <param name="standingOrders">The standing orders the provider reports now.</param>
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
    /// <param name="directDebits">The direct debits the provider reports now.</param>
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
    /// <param name="type">What was synced.</param>
    /// <param name="occurredAt">When the sync finished.</param>
    public SyncLogEntry RecordSync(SyncType type, DateTime occurredAt) => new(Id, type, occurredAt);

    /// <summary>
    /// Records that its past closing balances have been worked out, see <see cref="HistoryBackfilled"/>.
    /// </summary>
    public void MarkHistoryBackfilled() => HistoryBackfilled = true;

    /// <summary>
    /// Takes the account's first snapshot for <paramref name="day"/>, from its stored balance. Later syncs that day
    /// update it with <see cref="BalanceSnapshot.Refresh"/>.
    /// </summary>
    /// <param name="day">The day it's the balance for.</param>
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
