using System.Text;
using FinanceTracker.Domain.OpenBanking.Transactions;

namespace FinanceTracker.Domain.Common;

/// <summary>
/// Who a transaction is with, steady enough to tell when payments repeat, see
/// <see cref="Bills.SubscriptionDetector"/> and <see cref="PayPeriods.PayDetector"/>.
/// </summary>
public static class PayeeNames
{
    /// <summary>
    /// The merchant or counterparty the bank named, otherwise the description. Words with a digit in are dropped as
    /// they're usually a reference that changes each time, e.g. <c>NETFLIX.COM 866-579</c> or
    /// <c>ACME PAYROLL REF00P2118CXWF4</c>. When that leaves nothing, e.g. <c>7ELEVEN</c>, only the digits go.
    /// </summary>
    public static string Name(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        string name = transaction.Metadata.MerchantName
                      ?? transaction.Metadata.CounterPartyPreferredName
                      ?? transaction.Description;

        string[] words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        List<string> withoutReferences = words.Where(static x => !x.Any(char.IsDigit)).ToList();
        if (withoutReferences.Count > 0)
        {
            return Tidy(withoutReferences);
        }

        return Tidy(words.Select(WithoutDigits).Where(static x => x.Length > 0).ToList());
    }

    /// <summary>
    /// Upper case letters only, so <c>Netflix.com</c> and <c>NETFLIX COM</c> are the same payee.
    /// </summary>
    public static string Key(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        StringBuilder builder = new(name.Length);
        foreach (char character in name)
        {
            if (char.IsLetter(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        return builder.ToString();
    }

    private static string WithoutDigits(string word)
    {
        StringBuilder builder = new(word.Length);
        foreach (char character in word)
        {
            if (!char.IsDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string Tidy(List<string> words) => string.Join(' ', words).Trim(' ', '-', '*', '#', '/', '.');
}
