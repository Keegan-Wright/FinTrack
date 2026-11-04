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
    /// The merchant or counterparty the bank named, otherwise the description. Digits are dropped as they're usually
    /// a reference that changes each time, e.g. <c>NETFLIX.COM 866-579</c>.
    /// </summary>
    public static string Name(BankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        string name = transaction.Metadata.MerchantName
                      ?? transaction.Metadata.CounterPartyPreferredName
                      ?? transaction.Description;

        StringBuilder builder = new(name.Length);
        foreach (char character in name)
        {
            if (!char.IsDigit(character))
            {
                builder.Append(character);
            }
        }

        string[] words = builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words).Trim(' ', '-', '*', '#', '/', '.');
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
}
