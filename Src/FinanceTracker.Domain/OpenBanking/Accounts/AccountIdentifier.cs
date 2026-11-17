namespace FinanceTracker.Domain.OpenBanking.Accounts;

/// <summary>
/// How banks name an account, a UK sort code and number, an IBAN, or both. Banks name the other account of a transfer
/// this way in its reference, which is how a payment to one of the user's own accounts is recognised.
/// </summary>
/// <remarks>
/// Sort code and number are digits only, the IBAN upper case without spaces. A UK IBAN contains the sort code and
/// number, so they're read from it when the provider only sends the IBAN.
/// </remarks>
public sealed record AccountIdentifier
{
    // GB, 2 check digits, 4 letter bank code, then the sort code and account number
    /// <summary>
    /// How long a UK IBAN is.
    /// </summary>
    private const int UkIbanLength = 22;
    /// <summary>
    /// Where the sort code starts in a UK IBAN, after the country, check digits and bank code.
    /// </summary>
    private const int UkIbanSortCodeStart = 8;
    /// <summary>
    /// How many digits a sort code has.
    /// </summary>
    private const int SortCodeLength = 6;
    /// <summary>
    /// How many digits a UK account number has.
    /// </summary>
    private const int UkAccountNumberLength = 8;

    /// <param name="sortCode">The sort code, digits only.</param>
    /// <param name="number">The account number, digits only.</param>
    /// <param name="iban">The IBAN, upper case letters and digits only.</param>
    private AccountIdentifier(string? sortCode, string? number, string? iban)
    {
        SortCode = sortCode;
        Number = number;
        Iban = iban;
    }

    /// <summary>
    /// Set with <see cref="Number"/>, both or neither.
    /// </summary>
    public string? SortCode { get; }

    /// <summary>
    /// The account number, set with <see cref="SortCode"/>.
    /// </summary>
    public string? Number { get; }

    /// <summary>
    /// The IBAN, when the provider gives one.
    /// </summary>
    public string? Iban { get; }

    /// <summary>
    /// Whether there's a sort code or IBAN to go on. An account the provider gives neither for is still kept as
    /// <see cref="Unknown"/>, so this is how to tell.
    /// </summary>
    public bool IsKnown => SortCode is not null || Iban is not null;

    /// <summary>
    /// For an account the provider gives no sort code, number or IBAN for. A new one each time, as each account keeps
    /// its own.
    /// </summary>
    public static AccountIdentifier Unknown() => new(null, null, null);

    /// <summary>
    /// The identifier from the provider's values, which may be formatted e.g. <c>11-22-33</c>. Null when there's
    /// neither a sort code and number nor an IBAN, as for a card.
    /// </summary>
    /// <param name="sortCode">The sort code as the provider gives it.</param>
    /// <param name="number">The account number as the provider gives it.</param>
    /// <param name="iban">The IBAN as the provider gives it.</param>
    public static AccountIdentifier? Create(string? sortCode, string? number, string? iban)
    {
        string? sortCodeDigits = NullIfEmpty(Digits(sortCode));
        string? numberDigits = NullIfEmpty(Digits(number));
        string? normalisedIban = NullIfEmpty(Alphanumeric(iban));

        if (sortCodeDigits is null || numberDigits is null)
        {
            (sortCodeDigits, numberDigits) = FromUkIban(normalisedIban);
        }

        return sortCodeDigits is null && normalisedIban is null
            ? null
            : new AccountIdentifier(sortCodeDigits, numberDigits, normalisedIban);
    }

    /// <summary>
    /// Whether <paramref name="reference"/> names this account, by its IBAN or by the sort code followed by the
    /// number however they're separated, e.g. <c>11-22-33 12345678</c>.
    /// </summary>
    /// <param name="reference">A transaction's reference.</param>
    /// <remarks>
    /// Banks format references differently (<c>11-22-33 12345678</c>, <c>112233 12345678</c>, an IBAN in groups of
    /// four) and add other text around them, so both sides are reduced to the same characters and the reference only
    /// has to contain the account. The sort code and number together are 14 digits, too long to turn up by chance.
    /// </remarks>
    public bool IsNamedIn(string? reference) =>
        (SortCode is not null && Digits(reference).Contains(SortCode + Number, StringComparison.Ordinal)) ||
        (Iban is not null && Alphanumeric(reference).Contains(Iban, StringComparison.Ordinal));

    /// <summary>
    /// Whether <paramref name="other"/> is the same account, by sort code and number when both have them, otherwise by
    /// IBAN. Two people connecting a joint account each get it with the same identifier.
    /// </summary>
    /// <param name="other">The identifier to compare with.</param>
    public bool IsSameAccountAs(AccountIdentifier? other)
    {
        if (other is null)
        {
            return false;
        }

        if (SortCode is not null && other.SortCode is not null)
        {
            return SortCode == other.SortCode && Number == other.Number;
        }

        return Iban is not null && Iban == other.Iban;
    }

    // Only a 22 character GB IBAN whose last 14 characters are all digits, anything else (another country's, or a
    // malformed one) keeps its sort code and number unknown
    /// <summary>
    /// The sort code and account number inside a UK IBAN. Both null for any other IBAN.
    /// </summary>
    /// <param name="iban">The IBAN, upper case letters and digits only.</param>
    private static (string? SortCode, string? Number) FromUkIban(string? iban) =>
        iban is { Length: UkIbanLength } && iban.StartsWith("GB", StringComparison.Ordinal) &&
        iban.AsSpan(UkIbanSortCodeStart).ContainsAnyExceptInRange('0', '9') is false
            ? (iban.Substring(UkIbanSortCodeStart, SortCodeLength),
                iban.Substring(UkIbanSortCodeStart + SortCodeLength, UkAccountNumberLength))
            : (null, null);

    /// <summary>
    /// The digits in <paramref name="value"/>, empty when it's null.
    /// </summary>
    /// <param name="value">The value as the provider gives it.</param>
    private static string Digits(string? value) =>
        value is null ? string.Empty : string.Concat(value.Where(char.IsAsciiDigit));

    /// <summary>
    /// The letters and digits in <paramref name="value"/> in upper case, empty when it's null.
    /// </summary>
    /// <param name="value">The value as the provider gives it.</param>
    private static string Alphanumeric(string? value) =>
        value is null ? string.Empty : string.Concat(value.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();

    /// <summary>
    /// The value, or null when it's empty.
    /// </summary>
    /// <param name="value">The value.</param>
    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
