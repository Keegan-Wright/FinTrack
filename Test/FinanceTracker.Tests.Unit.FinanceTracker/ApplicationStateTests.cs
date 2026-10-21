using System.ComponentModel;

namespace FinanceTracker.Tests.Unit.FinanceTracker;

public class ApplicationStateTests
{
    [Test]
    public async Task Loading_Changed_RaisesPropertyChanged()
    {
        // Arrange
        var subject = new ApplicationState();
        List<string?> changed = [];
        subject.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act
        subject.Loading = true;

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(subject.Loading).IsTrue();
            await Assert.That(changed).IsEquivalentTo([nameof(ApplicationState.Loading)]);
        }
    }

    [Test]
    public async Task LoadingMessage_Changed_RaisesPropertyChanged()
    {
        // Arrange
        var subject = new ApplicationState();
        List<string?> changed = [];
        subject.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act
        subject.LoadingMessage = "Syncing";

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(subject.LoadingMessage).IsEqualTo("Syncing");
            await Assert.That(changed).IsEquivalentTo([nameof(ApplicationState.LoadingMessage)]);
        }
    }

    [Test]
    public async Task SettingSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var subject = new ApplicationState { Loading = true, LoadingMessage = "Syncing" };
        List<string?> changed = [];
        subject.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        // Act
        subject.Loading = true;
        subject.LoadingMessage = "Syncing";

        // Assert
        await Assert.That(changed.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PropertyChanged_WithoutSubscribers_DoesNotThrow()
    {
        var subject = new ApplicationState();

        subject.Loading = true;
        subject.LoadingMessage = null;

        await Assert.That(subject.Loading).IsTrue();
    }

    [Test]
    public async Task ApplicationState_IsNotifyPropertyChanged()
    {
        await Assert.That(new ApplicationState()).IsAssignableTo<INotifyPropertyChanged>();
    }
}
