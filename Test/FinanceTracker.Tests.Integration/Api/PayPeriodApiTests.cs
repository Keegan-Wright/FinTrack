using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Domain.OpenBanking.Transactions;
using FinanceTracker.Domain.PayPeriods;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

public class PayPeriodApiTests : IntegrationTestBase
{
    [Test]
    public async Task ChooseIncome_FollowsItsPayAndCanBeTurnedOffAndOn()
    {
        // Arrange: paid on the last working day of each of the last three months
        SignedInUser user = await RegisterUserAsync();
        DateOnly thisMonth = DateOnly.FromDateTime(DateTime.Now);
        thisMonth = new DateOnly(thisMonth.Year, thisMonth.Month, 1);
        DateOnly[] payDays = [.. Enumerable.Range(1, 3).Select(x => thisMonth.AddMonths(-x))
            .Select(static x => WorkingDays.LastIn(x.Year, x.Month))];
        await SeedAccountAsync(user, "main")
            .Do(s => s.TransactionScenario.WithTransaction("first", "main", Pay(payDays[2])))
            .Do(s => s.TransactionScenario.WithTransaction("second", "main", Pay(payDays[1])))
            .Do(s => s.TransactionScenario.WithTransaction("third", "main", Pay(payDays[0])));
        IPayPeriodsService service = ApiFor(user).Get<IPayPeriodsService>();

        // Act
        YourPay before = await service.GetYourPayAsync(CancellationToken);
        Income income = await service.GetIncomesAsync(CancellationToken).SingleAsync(CancellationToken);
        YourPay chosen = await service.ChooseAsync(new ChooseIncome { AccountId = income.AccountId, Key = income.Key },
            CancellationToken);
        YourPay off = await service.TurnOffAsync(CancellationToken);
        YourPay on = await service.TurnOnAsync(CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(before.Pay).IsNull();
            await Assert.That(income.Kind).IsEqualTo(PayDayKind.LastWorkingDay);
            await Assert.That(chosen.FollowsPay).IsTrue();
            await Assert.That(chosen.Pay!.LastPaidOn).IsEqualTo(payDays[0]);
            await Assert.That(off.FollowsPay).IsFalse();
            await Assert.That(on.FollowsPay).IsTrue();
        }
    }

    [Test]
    public async Task ChooseIncome_NotComingInLikePay_IsNotFound()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IPayPeriodsService service = ApiFor(user).Get<IPayPeriodsService>();

        // Act
        Func<Task> act = () => service.ChooseAsync(new ChooseIncome { AccountId = Guid.NewGuid(), Key = "SOMEONE" },
            CancellationToken);

        // Assert
        await Assert.That(act).Throws<InvalidOperationException>();
    }

    private Task<TestingScenario> SeedAccountAsync(SignedInUser user, string account) =>
        Scenario.Do(s => s.BankConnectionScenario.WithConnection($"{account}-bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount(account, $"{account}-bank"));

    private static TransactionDetails Pay(DateOnly day) =>
        new(Guid.NewGuid().ToString(), "ACME LTD", TransactionType.Credit, "CREDIT", 2400m, "GBP",
            day.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Utc), false, [], TransactionMetadata.None, false);
}
