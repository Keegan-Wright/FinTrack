using System.Net;
using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Integration.Http;

namespace FinanceTracker.Tests.Integration.Api;

/// <summary>
/// The browser's error reports through the API, as the app's error reporter sends them.
/// </summary>
public class ClientErrorApiTests : IntegrationTestBase
{
    [Test]
    public async Task Report_FromASignedInUser_IsTaken()
    {
        // Arrange
        IClientErrorsService service = ApiFor(await RegisterUserAsync()).Get<IClientErrorsService>();

        // Act & Assert, sent without an error
        await service.ReportAsync(Error(new string('m', 20)), CancellationToken);
    }

    [Test]
    public async Task Report_TooLong_IsRefused()
    {
        // Arrange
        IClientErrorsService service = ApiFor(await RegisterUserAsync()).Get<IClientErrorsService>();

        // Act
        HttpRequestException? refused = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.ReportAsync(Error(new string('m', ClientError.MaxMessageLength + 1)), CancellationToken));

        // Assert
        await Assert.That(refused!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    private static ClientError Error(string message) => new()
    {
        Message = message,
        ExceptionType = "System.InvalidOperationException",
        StackTrace = "at Budgets.OnInitializedAsync()",
        Category = "Microsoft.AspNetCore.Components.ErrorBoundary",
        Page = "/Budgets"
    };
}
