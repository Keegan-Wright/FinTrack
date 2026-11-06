namespace FinanceTracker.Tests.Integration.Http;

public sealed record SignedInUser(string Username, Guid UserId, FinTrackClient Client)
{
    public const string DefaultPassword = "Integration-Test-Password-1!";

    /// <summary>
    /// How far the user's clock is from the real one, nothing unless they were registered on a day of the test's
    /// choosing, see <c>IntegrationTestBase.RegisterUserAsync</c>.
    /// </summary>
    public TimeSpan ClockOffset { get; init; }
}
