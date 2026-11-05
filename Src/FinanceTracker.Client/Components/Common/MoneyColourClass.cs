namespace FinanceTracker.Client.Components.Common;

/// <summary>
/// The classes in <c>App.css</c> that colour money in the <see cref="FinanceTracker.Design.MoneyColours"/>, which
/// <see cref="MoneyColourStyles"/> sets as CSS variables.
/// </summary>
public static class MoneyColourClass
{
    /// <summary>Money coming in.</summary>
    public const string In = "ft-money--in";

    /// <summary>Money going out.</summary>
    public const string Out = "ft-money--out";

    /// <summary>
    /// <see cref="In"/> above zero, <see cref="Out"/> below it, nothing for zero.
    /// </summary>
    public static string? BySign(decimal amount)
    {
        if (amount > 0)
        {
            return In;
        }

        if (amount < 0)
        {
            return Out;
        }

        return null;
    }
}
