using FinanceTracker.Contracts.PayPeriods;

namespace FinanceTracker.Contracts.Services;

/// <summary>
/// The signed in user's pay periods, from one pay to the next. The pay is picked up by each sync from money that
/// comes in like pay, the largest unless the user chooses another, and periods are calendar months until one is found
/// or while the user has them turned off.
/// </summary>
public interface IPayPeriodsService
{
    /// <summary>
    /// The user's pay and the period today is in.
    /// </summary>
    Task<YourPay> GetYourPayAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Everything coming in like pay in the last three months, the largest first, to choose from.
    /// </summary>
    IAsyncEnumerable<Income> GetIncomesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The user says which income is their pay, which turns pay periods on. Kept even when another comes in like pay.
    /// </summary>
    /// <exception cref="InvalidOperationException">Nothing coming in like pay matches
    /// <paramref name="income"/>.</exception>
    Task<YourPay> ChooseAsync(ChooseIncome income, CancellationToken cancellationToken);

    /// <summary>
    /// Back to calendar months. The pay is still followed, so turning them on again has every pay.
    /// </summary>
    Task<YourPay> TurnOffAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Periods from pay to pay again.
    /// </summary>
    /// <exception cref="InvalidOperationException">No pay has been found yet.</exception>
    Task<YourPay> TurnOnAsync(CancellationToken cancellationToken);
}
