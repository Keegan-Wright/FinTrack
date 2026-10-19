using FinanceTracker.Infrastructure;
using FinanceTracker.Infrastructure.Calendar;
using FinanceTracker.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Calendar;

public class CalendarServiceTests : ServiceTestsFixtureBase
{
    [Test]
    public async Task GetMonthItemsAsync_ReturnsDayEntriesAndIncludesTransactionsOnMatchingDate()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"calendar-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        DateTime txDate = new DateTime(2026, 5, 15, 12, 0, 0, DateTimeKind.Utc);
        await SeedAccountAsync(factory, PrimaryUserId, "Main", _cancellationTokenSource.Token,
            Transaction("cal-1", "CalendarTx", -12, txDate, category: "SHOPPING"));

        var subject = new CalendarService(BuildUser(PrimaryUserId), factory, NullLogger<CalendarService>.Instance);

        List<(DateTime Date, int Transactions)> days = [];
        await foreach (var item in subject.GetMonthItemsAsync(5, 2026, _cancellationTokenSource.Token))
        {
            days.Add((item.Date, item.Transactions.Count()));
        }

        using (Assert.Multiple())
        {
            await Assert.That(days.Count).IsEqualTo(31);
            await Assert.That(days.Single(x => x.Date.Date == txDate.Date).Transactions).IsEqualTo(1);
        }
    }

    [Test]
    public async Task GetMonthItemsAsync_WhenUserHasNoTransactions_ReturnsZeroTransactionsForAllDays()
    {
        IDbContextFactory<FinanceTrackerContext> factory = BuildFactory($"calendar-empty-{Guid.NewGuid()}");
        await SeedUsersAsync(factory, _cancellationTokenSource.Token);

        var subject = new CalendarService(BuildUser(PrimaryUserId), factory, NullLogger<CalendarService>.Instance);

        var days = new List<int>();
        await foreach (var item in subject.GetMonthItemsAsync(5, 2026, _cancellationTokenSource.Token))
        {
            days.Add(item.Transactions.Count());
        }

        using (Assert.Multiple())
        {
            await Assert.That(days.Count).IsEqualTo(31);
            await Assert.That(days.All(static x => x == 0)).IsTrue();
        }
    }
}
