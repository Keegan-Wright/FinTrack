namespace FinanceTracker.Tests.Integration.Http;

public sealed record SignedInUser(string Username, Guid UserId, FinTrackClient Client)
{
    public const string DefaultPassword = "Integration-Test-Password-1!";
}
