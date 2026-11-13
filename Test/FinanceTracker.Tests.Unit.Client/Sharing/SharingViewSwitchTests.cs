using Bunit;
using FinanceTracker.Client;
using FinanceTracker.Client.Components.Common;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Sharing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Client.Sharing;

public class SharingViewSwitchTests : ComponentTestBase
{
    private readonly ISharingService _sharingService = Substitute.For<ISharingService>();
    private readonly ApplicationState _applicationState = new();

    public SharingViewSwitchTests() => Services.AddSingleton(_sharingService);

    [Test]
    public async Task NothingSharedWithThem_ShowsNoSwitch()
    {
        // Arrange
        _sharingService.GetViewAsync(Arg.Any<CancellationToken>())
            .Returns(new SharingView { IncludesShared = true, HasSharedAccounts = false });

        // Act
        IRenderedComponent<SharingViewSwitch> cut = Render();

        // Assert
        await Assert.That(cut.Markup.Trim()).IsEmpty();
    }

    [Test]
    public async Task SomethingShared_OffersEveryoneAndJustMine()
    {
        // Arrange
        _sharingService.GetViewAsync(Arg.Any<CancellationToken>())
            .Returns(new SharingView { IncludesShared = true, HasSharedAccounts = true });

        // Act
        IRenderedComponent<SharingViewSwitch> cut = Render();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(cut.Markup).Contains("Everyone");
            await Assert.That(cut.Markup).Contains("Just mine");
        }
    }

    [Test]
    public async Task ChoosingJustMine_SavesItAndRefreshesThePage()
    {
        // Arrange
        _sharingService.GetViewAsync(Arg.Any<CancellationToken>())
            .Returns(new SharingView { IncludesShared = true, HasSharedAccounts = true });
        _sharingService.ChangeViewAsync(Arg.Any<ChangeSharingView>(), Arg.Any<CancellationToken>())
            .Returns(new SharingView { IncludesShared = false, HasSharedAccounts = true });
        int refreshed = 0;
        _applicationState.Synced += () =>
        {
            refreshed++;
            return Task.CompletedTask;
        };
        IRenderedComponent<SharingViewSwitch> cut = Render();

        // Act
        await cut.InvokeAsync(() => cut.Instance.ChangeAsync(false));

        // Assert
        using (Assert.Multiple())
        {
            await _sharingService.Received(1).ChangeViewAsync(Arg.Is<ChangeSharingView>(x => !x.IncludeShared),
                Arg.Any<CancellationToken>());
            await Assert.That(refreshed).IsEqualTo(1);
        }
    }

    private IRenderedComponent<SharingViewSwitch> Render() =>
        Render<SharingViewSwitch>(p => p.AddCascadingValue(_applicationState));
}
