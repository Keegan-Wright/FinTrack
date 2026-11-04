using FinanceTracker.Contracts.Account;

namespace FinanceTracker.Client.Components.Accounts;

/// <summary>
/// One bank's accounts on the accounts page, see <see cref="ProviderAccounts"/>.
/// </summary>
/// <param name="Name">The bank's name.</param>
/// <param name="Accounts">In the order shown.</param>
public sealed record ProviderGroup(string Name, byte[] Logo, IReadOnlyList<AccountAndTransactions> Accounts);
