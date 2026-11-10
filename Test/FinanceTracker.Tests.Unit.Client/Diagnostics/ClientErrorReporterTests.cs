using FinanceTracker.Client.Diagnostics;
using FinanceTracker.Contracts.Diagnostics;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Unit.Client.Diagnostics;

public class ClientErrorReporterTests : ComponentTestBase
{
    private readonly IClientErrorsService _service = Substitute.For<IClientErrorsService>();
    private readonly List<ClientError> _sent = [];

    public ClientErrorReporterTests()
    {
        Services.AddSingleton(_service);
        Services.AddSingleton<ClientErrorReporter>();
        _service.ReportAsync(Arg.Do<ClientError>(_sent.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        Services.GetRequiredService<NavigationManager>().NavigateTo("Budgets?month=2026-11");
    }

    private ClientErrorReporter Reporter => Services.GetRequiredService<ClientErrorReporter>();

    [Test]
    public async Task ErrorLogged_SendsItWithThePageWithoutItsQuery()
    {
        // Arrange
        InvalidOperationException exception = Thrown(new InvalidOperationException("Budget not found"));

        // Act
        Log("Microsoft.AspNetCore.Components.ErrorBoundary", LogLevel.Error, "Unhandled exception", exception);
        await Reporter.WhenSentAsync();

        // Assert
        ClientError sent = _sent.Single();
        using (Assert.Multiple())
        {
            await Assert.That(sent.Message).IsEqualTo("Budget not found");
            await Assert.That(sent.ExceptionType).IsEqualTo("System.InvalidOperationException");
            await Assert.That(sent.StackTrace).Contains(nameof(Thrown));
            await Assert.That(sent.Category).IsEqualTo("Microsoft.AspNetCore.Components.ErrorBoundary");
            await Assert.That(sent.Page).IsEqualTo("/Budgets");
        }
    }

    [Test]
    public async Task WarningLogged_SendsNothing()
    {
        // Act
        Log("Page", LogLevel.Warning, "Slow to load");
        await Reporter.WhenSentAsync();

        // Assert
        await Assert.That(_sent).IsEmpty();
    }

    [Test]
    public async Task SameErrorAgain_SendsItOnce()
    {
        // Act
        Log("Page", LogLevel.Error, "Couldn't render");
        Log("Page", LogLevel.Error, "Couldn't render");
        await Reporter.WhenSentAsync();

        // Assert
        await Assert.That(_sent).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ManyErrors_StopsAtTheLimit()
    {
        // Act
        for (int error = 0; error < ClientErrorReporter.MaxReports + 5; error++)
        {
            Log("Page", LogLevel.Error, $"Error {error}");
        }

        await Reporter.WhenSentAsync();

        // Assert
        await Assert.That(_sent).Count().IsEqualTo(ClientErrorReporter.MaxReports);
    }

    [Test]
    public async Task SendingFails_DropsItWithoutReportingThat()
    {
        // Arrange, sending logs an error then fails, as a refused call would
        _service.ReportAsync(Arg.Any<ClientError>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Log("System.Net.Http.HttpClient", LogLevel.Error, "The server refused it");
            throw new HttpRequestException("refused");
        });

        // Act
        Log("Page", LogLevel.Error, "Couldn't render");
        await Reporter.WhenSentAsync();

        // Assert, the one report only, its own failure wasn't sent in turn
        await _service.Received(1).ReportAsync(Arg.Any<ClientError>(), Arg.Any<CancellationToken>());
    }

    // Logged the way Blazor and the framework log, straight through ILogger
    private void Log(string category, LogLevel level, string message, Exception? exception = null) =>
        new ClientErrorLoggerProvider(Services).CreateLogger(category)
            .Log(level, default, message, exception, static (state, _) => state);

    // Thrown so it has a stack trace, as a real one does
    private static T Thrown<T>(T exception) where T : Exception
    {
        try
        {
            throw exception;
        }
        catch (T thrown)
        {
            return thrown;
        }
    }
}
