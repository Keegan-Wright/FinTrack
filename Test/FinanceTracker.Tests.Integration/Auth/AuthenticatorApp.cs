using FinanceTracker.Infrastructure.Account.Authenticator;

namespace FinanceTracker.Tests.Integration.Auth;

/// <summary>
/// Makes the codes an authenticator app shows from the key it was given, with the app's own
/// <see cref="Totp"/> and <see cref="AuthenticatorSettings"/> so the two can't drift apart.
/// </summary>
public static class AuthenticatorApp
{
    /// <summary>
    /// The code for <paramref name="key"/> now, or <paramref name="stepsFromNow"/> steps away. Each code is taken
    /// once, so a test that turns two-factor on and then signs in uses the next step's for the second.
    /// </summary>
    /// <param name="key">The key as the profile shows it, spaces and case as they come.</param>
    /// <param name="stepsFromNow">How many steps on, negative for earlier ones.</param>
    public static string Code(string key, int stepsFromNow = 0) => CodeAt(key, CurrentStep() + stepsFromNow);

    /// <summary>
    /// A code that isn't right for <paramref name="key"/> in any step taken now.
    /// </summary>
    /// <param name="key">The key as the profile shows it.</param>
    public static string WrongCode(string key)
    {
        long step = CurrentStep();
        HashSet<string> taken = [];
        for (long drift = -AuthenticatorSettings.AllowedDriftSteps; drift <= AuthenticatorSettings.AllowedDriftSteps;
             drift++)
        {
            taken.Add(CodeAt(key, step + drift));
        }

        return new[] { "000000", "111111", "222222", "333333" }.First(x => !taken.Contains(x));
    }

    /// <summary>
    /// The code for <paramref name="key"/> in <paramref name="step"/>.
    /// </summary>
    private static string CodeAt(string key, long step) => Totp.Code(Base32.Decode(key), step);

    /// <summary>
    /// The step now.
    /// </summary>
    private static long CurrentStep() => Totp.StepAt(DateTimeOffset.UtcNow);
}
