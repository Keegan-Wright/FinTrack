using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Classification;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinanceTracker.Infrastructure.Tests.Services.Classification;

public class ClassificationServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task AddCustomClassificationAsync_ThenGetAllCustomClassificationsAsync_ReturnsAddedClassification()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"classification-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var subject = new ClassificationService(BuildUser(PrimaryUserId), factory,
            NullLogger<ClassificationService>.Instance);

        var added = await subject.AddCustomClassificationAsync(new AddClassifications
        {
            Tag = "Groceries"
        }, _cancellationTokenSource.Token);

        List<string> tags = [];
        await foreach (var item in subject.GetAllCustomClassificationsAsync(_cancellationTokenSource.Token))
        {
            tags.Add(item.Tag);
        }

        using (Assert.Multiple())
        {
            await Assert.That(added.Tag).IsEqualTo("Groceries");
            await Assert.That(tags).Contains("Groceries");
        }
    }

    [Test]
    public async Task GetAllCustomClassificationsAsync_WhenUserHasNone_ReturnsEmpty()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"classification-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var subject = new ClassificationService(BuildUser(PrimaryUserId), factory,
            NullLogger<ClassificationService>.Instance);

        var tags = new List<string>();
        await foreach (var item in subject.GetAllCustomClassificationsAsync(_cancellationTokenSource.Token))
        {
            tags.Add(item.Tag);
        }

        await Assert.That(tags.Count).IsEqualTo(0);
    }
}
