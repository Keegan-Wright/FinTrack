using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Infrastructure.Diagnostics;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Diagnostics;

public class ClientErrorsServiceTests : InfrastructureTestBase
{
    private const string Username = "primary";

    [Test]
    public async Task ReportAsync_LogsItAsAnErrorWithTheUserAndPage()
    {
        // Arrange
        await Scenario.Do(s => s.UserScenario.WithUser(Username));
        CapturingLogger<ClientErrorsService> logger = new();
        ClientErrorsService subject = new(CurrentUserFor(Username), DbContextFactory, logger);

        // Act
        await subject.ReportAsync(new ClientError
        {
            Message = "Budget not found",
            ExceptionType = "System.InvalidOperationException",
            StackTrace = "at Budgets.OnInitializedAsync()",
            Category = "Microsoft.AspNetCore.Components.ErrorBoundary",
            Page = "/Budgets"
        }, CancellationToken);

        // Assert
        (LogLevel Level, string Message, Exception? Exception) entry = logger.Entries.Single();
        using (Assert.Multiple())
        {
            await Assert.That(entry.Level).IsEqualTo(LogLevel.Error);
            await Assert.That(entry.Message)
                .StartsWith($"The browser hit an error for user {Scenario.UserMappings[Username]} on /Budgets")
                .And.Contains("System.InvalidOperationException: Budget not found")
                .And.Contains("at Budgets.OnInitializedAsync()");
        }
    }
}
