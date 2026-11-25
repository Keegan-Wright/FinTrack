using System.ComponentModel;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;

namespace FinanceTracker.Ai.Tools.Transactions;

/// <summary>
/// Tools for finding single transactions.
/// </summary>
/// <param name="transactionsService">Reads the user's transactions, the same as the transactions page.</param>
/// <param name="timeProvider">The clock, whose time zone the days are in.</param>
/// <param name="pageLinks">Makes the links to the pages its results can be checked on.</param>
public sealed class TransactionTools(ITransactionsService transactionsService, TimeProvider timeProvider,
    PageLinks pageLinks)
{
    /// <summary>
    /// How many transactions a search returns when the model doesn't say.
    /// </summary>
    private const int DefaultLimit = 20;

    /// <summary>
    /// Finds transactions, newest first.
    /// </summary>
    /// <param name="text">Text to look for.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <param name="accountIds">Only these accounts.</param>
    /// <param name="tags">Only these tags.</param>
    /// <param name="limit">How many to return at most.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    [AiTool("search_transactions")]
    [Description("Finds the user's transactions, newest first. Use it to look at single payments, such as the " +
        "last time they paid someone. For totals use get_totals, which counts every transaction rather than a few.")]
    public async Task<TransactionSearch> SearchTransactionsAsync(
        [Description("Text in the description, such as a shop's name. Leave out for any.")] string? text = null,
        [Description("The first day, as yyyy-MM-dd. Leave out for no start.")] DateOnly? from = null,
        [Description("The last day, as yyyy-MM-dd. Leave out for up to today.")] DateOnly? to = null,
        [Description("Only these accounts, ids from list_accounts. Leave out for all.")] List<Guid>? accountIds = null,
        [Description("Only transactions with one of these tags, from list_tags. Leave out for all.")]
        List<string>? tags = null,
        [Description("How many to return, 1 to 50. Defaults to 20.")] int limit = DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        int take = Math.Clamp(limit, 1, ToolText.MostRows);
        FilterTransactions filter = ToolFilters.For(timeProvider, from, to, accountIds, tags, text);
        List<TransactionFound> found = [];
        bool more = false;
        await foreach (Transaction transaction in transactionsService
            .GetAllTransactionsAsync(filter, cancellationToken))
        {
            if (found.Count == take)
            {
                more = true;
                break;
            }

            found.Add(ToFound(transaction));
        }

        return new TransactionSearch(found, more, ToolText.FromBanks, pageLinks.For(ToolLinks.Transactions));
    }

    /// <summary>
    /// What a model sees of <paramref name="transaction"/>.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    private TransactionFound ToFound(Transaction transaction)
    {
        DateTime local = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(transaction.TransactionTime, DateTimeKind.Utc), timeProvider.LocalTimeZone);
        return new TransactionFound(DateOnly.FromDateTime(local), transaction.Description, transaction.Amount,
            transaction.Currency, transaction.AccountName, transaction.Tags.ToList(), transaction.Pending,
            !transaction.CountsAsIncomeOrSpending);
    }
}
