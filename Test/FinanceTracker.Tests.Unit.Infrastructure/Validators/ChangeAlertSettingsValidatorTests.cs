using FinanceTracker.Contracts.Notifications;
using FinanceTracker.Contracts.Validation;
using FinanceTracker.Tests.Unit.Shared;
using FluentValidation.Results;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class ChangeAlertSettingsValidatorTests : UnitTestBase
{
    private readonly ChangeAlertSettingsValidator _subject = new();

    [Test]
    public async Task ValidateAsync_AtTheEdges_IsValid()
    {
        // Act
        ValidationResult lowest = await _subject.ValidateAsync(new ChangeAlertSettings
        {
            BudgetWarningPercent = 1, BillReminderDays = 1, LargeTransactionOver = 0.01m
        }, CancellationToken);
        ValidationResult highest = await _subject.ValidateAsync(new ChangeAlertSettings
        {
            BudgetWarningPercent = 100, BillReminderDays = ChangeAlertSettings.MostBillReminderDays,
            LargeTransactionOver = null,
            LowBalance = [new LowBalanceThreshold { AccountId = Guid.CreateVersion7(), Below = -250m }]
        }, CancellationToken);

        // Assert, an overdraft can be the line too
        using (Assert.Multiple())
        {
            await Assert.That(lowest.IsValid).IsTrue();
            await Assert.That(highest.IsValid).IsTrue();
        }
    }

    [Test]
    public async Task ValidateAsync_OutOfRange_IsInvalid()
    {
        // Act
        ValidationResult result = await _subject.ValidateAsync(new ChangeAlertSettings
        {
            BudgetWarningPercent = 101, BillReminderDays = 0, LargeTransactionOver = 0m
        }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName)).IsEquivalentTo([
            nameof(ChangeAlertSettings.BudgetWarningPercent), nameof(ChangeAlertSettings.BillReminderDays),
            nameof(ChangeAlertSettings.LargeTransactionOver)
        ]);
    }

    [Test]
    public async Task ValidateAsync_AnAccountTwice_IsInvalid()
    {
        // Arrange
        Guid account = Guid.CreateVersion7();

        // Act
        ValidationResult result = await _subject.ValidateAsync(new ChangeAlertSettings
        {
            BudgetWarningPercent = 80,
            BillReminderDays = 3,
            LowBalance =
            [
                new LowBalanceThreshold { AccountId = account, Below = 100m },
                new LowBalanceThreshold { AccountId = account, Below = 50m }
            ]
        }, CancellationToken);

        // Assert
        await Assert.That(result.Errors.Select(static x => x.PropertyName))
            .IsEquivalentTo([nameof(ChangeAlertSettings.LowBalance)]);
    }
}
