using System.Net;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Infrastructure;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;
using Microsoft.EntityFrameworkCore;
using DomainDebt = FinanceTracker.Domain.Debts.Debt;

namespace FinanceTracker.Tests.Integration.Api;

public class DebtApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddDebt_CanBePaidOffReadForecastUpdatedAndDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        DateOnly today = DateOnly.FromDateTime(DateTime.Now);
        await SeedAccountAsync(user, "main")
            .Do(s => s.TransactionScenario.WithTransaction("payment", "main",
                Paid("CAR FINANCE", -100m, today.AddDays(-2))));
        IDebtsService service = ApiFor(user).Get<IDebtsService>();

        // Act
        Debt added = await service.AddDebtAsync(CarLoan(today.AddDays(-10)), CancellationToken);
        Transaction paid = await service.PayDebtAsync(Scenario.TransactionMappings["payment"],
            new PayDebt { DebtId = added.Id }, CancellationToken);
        DebtDetail read = await service.GetDebtAsync(added.Id, CancellationToken);
        DebtForecast payingMore = await service.ForecastAsync(added.Id, 300m, CancellationToken);
        SaveDebt renamed = CarLoan(today.AddDays(-10));
        renamed.Name = "Van loan";
        Debt updated = await service.UpdateDebtAsync(added.Id, renamed, CancellationToken);
        await service.DeleteDebtAsync(added.Id, CancellationToken);
        List<Debt> afterDelete = await service.GetDebtsAsync(CancellationToken).ToListAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(paid.Debt!.DebtName).IsEqualTo("Car loan");
            await Assert.That(read.Debt.Owed).IsEqualTo(900m);
            await Assert.That(read.Payments.Single().Amount).IsEqualTo(100m);
            await Assert.That(read.Debt.Forecast.Months).IsEqualTo(9);
            await Assert.That(payingMore.Months).IsEqualTo(3);
            await Assert.That(updated.Name).IsEqualTo("Van loan");
            await Assert.That(afterDelete).IsEmpty();
        }
    }

    [Test]
    public async Task AddDebt_Invalid_IsABadRequest()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IDebtsService service = ApiFor(user).Get<IDebtsService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AddDebtAsync(new SaveDebt { Name = "Empty" }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetDebt_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        await Scenario.Do(s => s.DebtScenario.WithDebt("loan", owner.Username));
        IDebtsService service = ApiFor(other).Get<IDebtsService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetDebtAsync(Scenario.DebtMappings["loan"], CancellationToken));
    }

    [Test]
    public async Task Forecast_NoPayment_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.DebtScenario.WithDebt("loan", user.Username));
        IDebtsService service = ApiFor(user).Get<IDebtsService>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ForecastAsync(Scenario.DebtMappings["loan"], 0m, CancellationToken));
    }

    [Test]
    public async Task Debts_AreStoredEncryptedAndReadBack()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IDebtsService service = ApiFor(user).Get<IDebtsService>();
        Debt added = await service.AddDebtAsync(CarLoan(new DateOnly(2026, 1, 1)), CancellationToken);

        // Act
        await using FinanceTrackerContext context = await DbContextFactory.CreateDbContextAsync(CancellationToken);
        string storedLender = await context.Database
            .SqlQueryRaw<string>("SELECT \"Lender\" AS \"Value\" FROM \"Debts\" WHERE \"Id\" = {0}", added.Id)
            .SingleAsync(CancellationToken);
        string storedBalance = await context.Database
            .SqlQueryRaw<string>("SELECT \"StartingBalance\" AS \"Value\" FROM \"Debts\" WHERE \"Id\" = {0}",
                added.Id)
            .SingleAsync(CancellationToken);
        DomainDebt loaded = await context.Debts.SingleAsync(x => x.Id == added.Id, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(storedLender).DoesNotContain("Car Finance Co");
            await Assert.That(storedBalance).DoesNotContain("1000");
            await Assert.That(loaded.Lender).IsEqualTo("Car Finance Co");
            await Assert.That(loaded.StartingBalance).IsEqualTo(1000m);
            await Assert.That(loaded.StartedOn).IsEqualTo(new DateOnly(2026, 1, 1));
        }
    }

    private Task<TestingScenario> SeedAccountAsync(SignedInUser user, string account) =>
        Scenario.Do(s => s.BankConnectionScenario.WithConnection($"{account}-bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount(account, $"{account}-bank"));

    private static SaveDebt CarLoan(DateOnly startedOn) => new()
    {
        Name = "Car loan",
        Lender = "Car Finance Co",
        StartingBalance = 1000m,
        StartedOn = startedOn,
        InterestRate = 0m,
        MonthlyPayment = 100m
    };

    private static TransactionDetails Paid(string description, decimal amount, DateOnly day) =>
        new(Guid.NewGuid().ToString(), description, TransactionType.Debit, "PURCHASE", amount, "GBP",
            day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc), false, [], TransactionMetadata.None, false);
}
