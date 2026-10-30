using System.Net;
using FinanceTracker.Contracts.Bills;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using DomainBill = FinanceTracker.Domain.Bills.Bill;
using DomainBillFrequency = FinanceTracker.Domain.Bills.BillFrequency;
using DomainBillSource = FinanceTracker.Domain.Bills.BillSource;

namespace FinanceTracker.Tests.Integration.Api;

public class BillApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddBill_CanBeReadShownForTheMonthEndedReopenedAndDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        DateTime now = DateTime.Now;
        DateOnly paidOn = DateOnly.FromDateTime(now).AddDays(-1);
        await SeedAccountAsync(user, "main")
            .Do(s => s.TransactionScenario.WithTransaction("payment", "main", Paid("STREAMCO", -9.99m, paidOn)));
        IBillsService service = ApiFor(user).Get<IBillsService>();

        // Act
        Bill added = await service.AddBillAsync(Streaming(Scenario.AccountMappings["main"], paidOn), CancellationToken);
        Bill read = await service.GetBillAsync(added.Id, CancellationToken);
        BillMonthOverview month = await service.GetMonthAsync(paidOn.Year, paidOn.Month, CancellationToken);
        Bill ended = await service.EndBillAsync(added.Id, CancellationToken);
        Bill reopened = await service.ReopenBillAsync(added.Id, CancellationToken);
        await service.DeleteBillAsync(added.Id, CancellationToken);
        List<Bill> afterDelete = await service.GetBillsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(read.Name).IsEqualTo("Streaming");
            await Assert.That(read.LastPaid).IsEqualTo(9.99m);
            await Assert.That(month.Bills.Single().Periods.Single().State).IsEqualTo(BillState.Paid);
            await Assert.That(ended.EndedByUser).IsTrue();
            await Assert.That(reopened.EndedOn).IsNull();
            await Assert.That(afterDelete).IsEmpty();
        }
    }

    [Test]
    public async Task AddBill_Invalid_IsABadRequest()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IBillsService service = ApiFor(user).Get<IBillsService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AddBillAsync(new SaveBill { Name = "Empty" }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task DeleteBill_FromAStandingOrder_IsAConflict()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await SeedAccountAsync(user, "main")
            .Do(s => s.BillScenario.WithBill("rent", user.Username, "main", static (userId, accountId) =>
                new DomainBill(userId, accountId, "Rent", "Landlord", DomainBillFrequency.Monthly, 650m,
                    new DateOnly(2026, 1, 15), DomainBillSource.StandingOrder, Guid.NewGuid())));
        IBillsService service = ApiFor(user).Get<IBillsService>();

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() =>
            service.DeleteBillAsync(Scenario.BillMappings["rent"], CancellationToken));
    }

    [Test]
    public async Task GetBill_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await SeedAccountAsync(owner, "main")
            .Do(s => s.BillScenario.WithBill("streaming", owner.Username, "main"));
        IBillsService service = ApiFor(other).Get<IBillsService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetBillAsync(Scenario.BillMappings["streaming"], CancellationToken));
    }

    [Test]
    public async Task ConfirmSuggestion_MakesABillPaidByItsPayments()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        await SeedAccountAsync(user, "main")
            .Do(s => s.TransactionScenario.WithTransaction("first", "main", Paid("STREAMCO", -9.99m,
                today.AddMonths(-2))))
            .Do(s => s.TransactionScenario.WithTransaction("second", "main", Paid("STREAMCO", -9.99m,
                today.AddMonths(-1))))
            .Do(s => s.TransactionScenario.WithTransaction("third", "main", Paid("STREAMCO", -9.99m, today)))
            .Do(s => s.BillScenario.WithSuggestion("streaming", user.Username, "main", "Streamco", 9.99m,
                today.AddMonths(-2), today, "first", "second", "third"));
        IBillsService service = ApiFor(user).Get<IBillsService>();

        // Act
        List<BillSuggestion> waiting = await service.GetSuggestionsAsync(CancellationToken)
            .ToListAsync(CancellationToken);
        Bill bill = await service.ConfirmSuggestionAsync(waiting.Single().Id, CancellationToken);
        int countAfter = await service.GetSuggestionCountAsync(CancellationToken);

        // Assert
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        int paying = await context.BankTransactions.CountAsync(x => x.BillPayment!.BillId == bill.Id,
            CancellationToken);
        using (Assert.Multiple())
        {
            await Assert.That(waiting.Single().Payments).Count().IsEqualTo(3);
            await Assert.That(bill.Source).IsEqualTo(BillSource.Detected);
            await Assert.That(paying).IsEqualTo(3);
            await Assert.That(countAfter).IsEqualTo(0);
        }
    }

    [Test]
    public async Task Bills_AreStoredEncryptedAndReadBack()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await SeedAccountAsync(user, "main");
        IBillsService service = ApiFor(user).Get<IBillsService>();
        Bill added = await service.AddBillAsync(Streaming(Scenario.AccountMappings["main"],
            new DateOnly(2026, 1, 12)), CancellationToken);

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string storedPayee = await context.Database
            .SqlQueryRaw<string>("SELECT \"Payee\" AS \"Value\" FROM \"Bills\" WHERE \"Id\" = {0}", added.Id)
            .SingleAsync(CancellationToken);
        string storedAmounts = await context.Database
            .SqlQueryRaw<string>("SELECT \"Amounts\"::text AS \"Value\" FROM \"Bills\" WHERE \"Id\" = {0}", added.Id)
            .SingleAsync(CancellationToken);
        string storedFrequency = await context.Database
            .SqlQueryRaw<string>("SELECT \"Frequency\" AS \"Value\" FROM \"Bills\" WHERE \"Id\" = {0}", added.Id)
            .SingleAsync(CancellationToken);
        DomainBill loaded = await context.Bills.SingleAsync(x => x.Id == added.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(storedPayee).DoesNotContain("Streamco");
            await Assert.That(storedAmounts).DoesNotContain("9.99");
            await Assert.That(storedFrequency).IsNotEqualTo("Monthly");
            await Assert.That(loaded.Frequency).IsEqualTo(DomainBillFrequency.Monthly);
            await Assert.That(loaded.Payee).IsEqualTo("Streamco");
            await Assert.That(loaded.ExpectedAmount).IsEqualTo(9.99m);
        }
    }

    private Task<TestingScenario> SeedAccountAsync(SignedInUser user, string account) =>
        Scenario.Do(s => s.BankConnectionScenario.WithConnection($"{account}-bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount(account, $"{account}-bank"));

    private static SaveBill Streaming(Guid accountId, DateOnly nextDue) => new()
    {
        Name = "Streaming",
        Payee = "Streamco",
        AccountId = accountId,
        Frequency = BillFrequency.Monthly,
        NextDue = nextDue,
        ExpectedAmount = 9.99m,
        TolerancePercent = 5
    };

    private static TransactionDetails Paid(string description, decimal amount, DateOnly day) =>
        new(Guid.NewGuid().ToString(), description, TransactionType.Debit, "PURCHASE", amount, "GBP",
            day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), false, [], TransactionMetadata.None, false);
}
