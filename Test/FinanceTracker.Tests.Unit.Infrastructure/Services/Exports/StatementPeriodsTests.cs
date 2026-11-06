using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure.Exports.Statements;
using FinanceTracker.Tests.Unit.Shared;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.Extensions.Logging.Abstractions;
using PayPeriod = FinanceTracker.Contracts.PayPeriods.PayPeriod;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Exports;

/// <summary>
/// The months there's a statement for, today's back to the one the oldest transaction is in, on 30 Oct 2026.
/// </summary>
public class StatementPeriodsTests : InfrastructureTestBase
{
    private const string Username = "primary";
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task GetAsync_NoTransactions_IsJustTodaysMonth()
    {
        // Arrange
        await SeedAsync();

        // Act
        List<PayPeriod> periods = await BuildSubject(TimeZoneInfo.Utc).GetAsync(CancellationToken);

        // Assert
        await Assert.That(periods.Select(static x => x.Month)).IsEquivalentTo([new DateOnly(2026, 10, 1)]);
    }

    [Test]
    public async Task GetAsync_CalendarMonths_GoBackToTheOldestTransactionNewestFirst()
    {
        // Arrange
        await SeedAsync().Do(s => s.TransactionScenario.WithTransaction("oldest", "main", Paid(8, 15, 12)));

        // Act
        List<PayPeriod> periods = await BuildSubject(TimeZoneInfo.Utc).GetAsync(CancellationToken);

        // Assert
        await Assert.That(periods.Select(static x => x.Month)).IsEquivalentTo(
            [new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 1), new DateOnly(2026, 8, 1)],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task GetAsync_PaidOnTheLastWorkingDay_RunsFromPayToPay()
    {
        // Arrange, today's pay on Friday 30 Oct starts November, the oldest transaction is in September's
        await SeedAsync()
            .Do(s => s.PayScenario.WithPayOnTheLastWorkingDay(Username, "main", new DateOnly(2026, 8, 28),
                new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30)))
            .Do(s => s.TransactionScenario.WithTransaction("oldest", "main", Paid(9, 3, 12)));

        // Act
        List<PayPeriod> periods = await BuildSubject(TimeZoneInfo.Utc).GetAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(periods.Select(static x => x.Month)).IsEquivalentTo(
                [new DateOnly(2026, 11, 1), new DateOnly(2026, 10, 1), new DateOnly(2026, 9, 1)],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(periods[0].Start).IsEqualTo(new DateOnly(2026, 10, 30));
            await Assert.That(periods[2].Start).IsEqualTo(new DateOnly(2026, 8, 28));
            await Assert.That(periods[2].End).IsEqualTo(new DateOnly(2026, 9, 29));
        }
    }

    [Test]
    public async Task GetAsync_OldestJustAfterMidnightInSummerTime_StartsWithThatDaysMonth()
    {
        // Arrange, 23:30 UTC on 31 Jul is 00:30 on 1 Aug in the UK
        TimeZoneInfo london = TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        await SeedAsync().Do(s => s.TransactionScenario.WithTransaction("oldest", "main", Paid(7, 31, 23, 30)));

        // Act
        List<PayPeriod> periods = await BuildSubject(london).GetAsync(CancellationToken);

        // Assert, August's the oldest, no July with nothing in it
        await Assert.That(periods.Last().Month).IsEqualTo(new DateOnly(2026, 8, 1));
    }

    private Task<TestingScenario> SeedAsync() =>
        Scenario.Do(s => s.UserScenario.WithUser(Username))
            .Do(s => s.BankConnectionScenario.WithConnection("bank", Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"));

    private StatementPeriods BuildSubject(TimeZoneInfo timeZone) =>
        new(CurrentUserFor(Username), DbContextFactory, NullLogger<StatementPeriods>.Instance,
            new FixedTimeProvider(Now, timeZone));

    private static TransactionDetails Paid(int month, int day, int hour, int minute = 0) =>
        new(Guid.NewGuid().ToString(), "CORNER SHOP", TransactionType.Debit, "PURCHASE", -5m, "GBP",
            new DateTime(2026, month, day, hour, minute, 0, DateTimeKind.Utc), false, [], TransactionMetadata.None,
            false);
}
