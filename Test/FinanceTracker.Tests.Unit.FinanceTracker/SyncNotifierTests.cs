using FinanceTracker.Api;
using FinanceTracker.Contracts.Api;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.FinanceTracker;

public class SyncNotifierTests : UnitTestBase
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly IHubContext<SyncHub> _hubContext = Substitute.For<IHubContext<SyncHub>>();
    private readonly SyncNotifier _subject;

    public SyncNotifierTests() => _subject = new SyncNotifier(_hubContext, NullLogger<SyncNotifier>.Instance);

    [Test]
    public async Task NotifyAsync_PushesToTheUsersBrowsers()
    {
        // Arrange
        IClientProxy userClients = Substitute.For<IClientProxy>();
        _hubContext.Clients.User(_userId.ToString()).Returns(userClients);

        // Act
        await _subject.NotifyAsync(_userId);

        // Assert
        await userClients.Received(1).SendCoreAsync(SyncHubContract.SyncComplete,
            Arg.Is<object?[]>(x => x.Length == 0), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NotifyAsync_WhenTheHubFails_DoesNotFailTheSync()
    {
        // Arrange
        _hubContext.Clients.User(_userId.ToString()).Returns(_ => throw new InvalidOperationException("hub down"));

        // Act & Assert
        await _subject.NotifyAsync(_userId);
    }
}
