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
    private const int UkIbanLength = 22;
    private const int UkIbanSortCodeStart = 8;
    private const int SortCodeLength = 6;
    private const int UkAccountNumberLength = 8;

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

    public string? Number { get; }

    public string? Iban { get; }

    /// <summary>
    /// The identifier from the provider's values, which may be formatted e.g. <c>11-22-33</c>. Null when there's
    /// neither a sort code and number nor an IBAN, as for a card.
    /// </summary>
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
    /// number however they're separated, e.g. <c>11-11-11 11111111</c>.
    /// </summary>
    /// <remarks>
    /// Banks format references differently (<c>11-11-11 11111111</c>, <c>11-11-11 11111111</c>, an IBAN in groups of
    /// four) and add other text around them, so both sides are reduced to the same characters and the reference only
    /// has to contain the account. The sort code and number together are 14 digits, too long to turn up by chance.
    /// </remarks>
    public bool IsNamedIn(string? reference) =>
        (SortCode is not null && Digits(reference).Contains(SortCode + Number, StringComparison.Ordinal)) ||
        (Iban is not null && Alphanumeric(reference).Contains(Iban, StringComparison.Ordinal));

    // Only a 22 character GB IBAN whose last 14 characters are all digits, anything else (another country's, or a
    // malformed one) keeps its sort code and number unknown
    private static (string? SortCode, string? Number) FromUkIban(string? iban) =>
        iban is { Length: UkIbanLength } && iban.StartsWith("GB", StringComparison.Ordinal) &&
        iban.AsSpan(UkIbanSortCodeStart).ContainsAnyExceptInRange('0', '9') is false
            ? (iban.Substring(UkIbanSortCodeStart, SortCodeLength),
                iban.Substring(UkIbanSortCodeStart + SortCodeLength, UkAccountNumberLength))
            : (null, null);

    private static string Digits(string? value) =>
        value is null ? string.Empty : string.Concat(value.Where(char.IsAsciiDigit));

    private static string Alphanumeric(string? value) =>
        value is null ? string.Empty : string.Concat(value.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();

    private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;
}
