using FinanceTracker.Contracts.PayPeriods;

namespace FinanceTracker.Tests.Unit.Client.PayPeriods;

/// <summary>
/// The user's pay as <see cref="Contracts.Services.IPayPeriodsService"/> gives it, for the pages that follow it.
/// </summary>
internal static class YourPays
{
    /// <summary>
    /// No pay found yet, so calendar months.
    /// </summary>
    public static YourPay NoneYet(int year, int month)
    {
        DateOnly first = new(year, month, 1);
        return new YourPay
        {
            CurrentPeriod = new PayPeriod { Start = first, End = first.AddMonths(1).AddDays(-1), Month = first }
        };
    }

    /// <summary>
    /// Paid on the last working day, today in the period from 30 Oct 2026, named November.
    /// </summary>
    public static YourPay FromPayToPay(bool off = false)
    {
        PayPeriod period = new()
        {
            Start = new DateOnly(2026, 10, 30), End = new DateOnly(2026, 11, 29), Month = new DateOnly(2026, 11, 1)
        };
        if (off)
        {
            period = NoneYet(2026, 11).CurrentPeriod;
        }

        return new YourPay
        {
            Pay = Pay("ACME LTD", 2400m),
            IsOff = off,
            FollowsPay = !off,
            CurrentPeriod = period
        };
    }

    public static Income Pay(string name, decimal amount) => new()
    {
        AccountId = Guid.NewGuid(),
        AccountName = "Main",
        Key = name.Replace(" ", string.Empty, StringComparison.Ordinal),
        Name = name,
        Amount = amount,
        Kind = PayDayKind.LastWorkingDay,
        LastPaidOn = new DateOnly(2026, 10, 30)
    };
}
