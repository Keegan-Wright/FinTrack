using FinanceTracker.Domain.Bills;

namespace FinanceTracker.Tests.Unit.Domain.Bills;

[Category("Domain")]
public class BillTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid AccountId = Guid.NewGuid();

    [Test]
    public async Task Constructor_ManualWithASourceId_Throws() =>
        await Assert.That(() => Build(BillFrequency.Monthly, new DateOnly(2026, 1, 15), BillSource.Manual,
            Guid.NewGuid())).Throws<ArgumentException>();

    [Test]
    public async Task Constructor_StandingOrderWithoutASourceId_Throws() =>
        await Assert.That(() => Build(BillFrequency.Monthly, new DateOnly(2026, 1, 15), BillSource.StandingOrder))
            .Throws<ArgumentException>();

    [Test]
    public async Task Constructor_ZeroAmount_Throws() =>
        await Assert.That(() => new Bill(UserId, AccountId, "Rent", "Landlord", BillFrequency.Monthly, 0,
            new DateOnly(2026, 1, 1), BillSource.Manual)).Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task DueDatesBetween_MonthlyOnThe31st_FallsOnTheLastDayOfShortMonthsWithoutDrifting()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 31));

        // Act
        List<DateOnly> due = bill.DueDatesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 30)).ToList();

        // Assert
        await Assert.That(due).IsEquivalentTo([
            new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 28), new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 30)
        ]);
    }

    [Test]
    [Arguments(BillFrequency.Weekly, 7)]
    [Arguments(BillFrequency.Fortnightly, 14)]
    [Arguments(BillFrequency.FourWeekly, 28)]
    public async Task DueDatesBetween_WeekBased_StepsByDays(BillFrequency frequency, int days)
    {
        // Arrange
        DateOnly start = new(2026, 1, 5);
        Bill bill = Build(frequency, start);

        // Act
        List<DateOnly> due = bill.DueDatesBetween(start, start.AddDays(days * 3)).ToList();

        // Assert
        await Assert.That(due).IsEquivalentTo([
            start, start.AddDays(days), start.AddDays(days * 2), start.AddDays(days * 3)
        ]);
    }

    [Test]
    public async Task DueDatesBetween_BeforeItStarts_LeavesThoseOut()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 3, 10));

        // Act
        List<DateOnly> due = bill.DueDatesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 4, 30)).ToList();

        // Assert
        await Assert.That(due).IsEquivalentTo([new DateOnly(2026, 3, 10), new DateOnly(2026, 4, 10)]);
    }

    [Test]
    public async Task DueDatesBetween_AfterItEnded_LeavesThoseOut()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10));
        bill.End(new DateOnly(2026, 2, 20));

        // Act
        List<DateOnly> due = bill.DueDatesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30)).ToList();

        // Assert
        await Assert.That(due).IsEquivalentTo([new DateOnly(2026, 1, 10), new DateOnly(2026, 2, 10)]);
    }

    [Test]
    public async Task DueDatesBetween_AnchorAfterTheRange_CountsBack()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Quarterly, new DateOnly(2025, 1, 15));
        bill.Reschedule(BillFrequency.Quarterly, new DateOnly(2027, 1, 15));

        // Act
        List<DateOnly> due = bill.DueDatesBetween(new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)).ToList();

        // Assert
        await Assert.That(due).IsEquivalentTo([
            new DateOnly(2026, 1, 15), new DateOnly(2026, 4, 15), new DateOnly(2026, 7, 15),
            new DateOnly(2026, 10, 15)
        ]);
    }

    [Test]
    public async Task NextDue_OnADueDate_IsThatDay() =>
        await Assert.That(Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10)).NextDue(new DateOnly(2026, 5, 10)))
            .IsEqualTo(new DateOnly(2026, 5, 10));

    [Test]
    public async Task NextDue_Yearly_FindsNextYears() =>
        await Assert.That(Build(BillFrequency.Yearly, new DateOnly(2026, 3, 1)).NextDue(new DateOnly(2026, 3, 2)))
            .IsEqualTo(new DateOnly(2027, 3, 1));

    [Test]
    public async Task NextDue_Ended_IsNull()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10));
        bill.End(new DateOnly(2026, 3, 1));

        // Act & Assert
        await Assert.That(bill.NextDue(new DateOnly(2026, 3, 2))).IsNull();
    }

    [Test]
    [Arguments(2026, 2, 7, 2026, 2, 10)]
    [Arguments(2026, 2, 14, 2026, 2, 10)]
    [Arguments(2026, 2, 26, 2026, 3, 10)]
    [Arguments(2025, 12, 12, 2026, 1, 10)]
    public async Task NearestDue_IsTheClosestDueDateNeverBeforeTheStart(int year, int month, int day, int dueYear,
        int dueMonth, int dueDay) =>
        await Assert.That(Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10))
                .NearestDue(new DateOnly(year, month, day)))
            .IsEqualTo(new DateOnly(dueYear, dueMonth, dueDay));

    [Test]
    public async Task ChangeAmount_FromADay_LeavesTheDaysBefore()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10), amount: 10.99m);

        // Act
        bool changed = bill.ChangeAmount(12.99m, new DateOnly(2026, 4, 11));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 4, 10))).IsEqualTo(10.99m);
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 5, 10))).IsEqualTo(12.99m);
            await Assert.That(bill.ExpectedAmount).IsEqualTo(12.99m);
        }
    }

    [Test]
    public async Task ChangeAmount_ToWhatItAlreadyIs_ChangesNothing()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10), amount: 10.99m);

        // Act
        bool changed = bill.ChangeAmount(10.99m, new DateOnly(2026, 4, 11));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(bill.Amounts).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task ChangeAmount_BeforeALaterChange_ReplacesIt()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10), amount: 10m);
        bill.ChangeAmount(12m, new DateOnly(2026, 6, 1));

        // Act
        bill.ChangeAmount(11m, new DateOnly(2026, 3, 1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 7, 10))).IsEqualTo(11m);
            await Assert.That(bill.Amounts).Count().IsEqualTo(2);
        }
    }

    [Test]
    [Arguments(100, true)]
    [Arguments(105, true)]
    [Arguments(95, true)]
    [Arguments(105.01, false)]
    [Arguments(94.99, false)]
    public async Task IsExpected_WithinTheDefaultTolerance(double paid, bool expected) =>
        await Assert.That(Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10), amount: 100m)
            .IsExpected((decimal)paid, new DateOnly(2026, 2, 10))).IsEqualTo(expected);

    [Test]
    public async Task IsExpected_NoTolerance_OnlyTheExactAmount()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10), amount: 100m);

        // Act
        bill.ChangeTolerance(0);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(bill.IsExpected(100m, new DateOnly(2026, 2, 10))).IsTrue();
            await Assert.That(bill.IsExpected(100.01m, new DateOnly(2026, 2, 10))).IsFalse();
        }
    }

    [Test]
    public async Task ChangeTolerance_Over100_Throws() =>
        await Assert.That(() => Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10)).ChangeTolerance(101))
            .Throws<ArgumentOutOfRangeException>();

    [Test]
    public async Task FollowPrice_ChangedPeriod_ExpectsTheNewPriceFromTheDayAfterItsDueDate()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10), amount: 10.99m);
        BillPeriod changed = new(new DateOnly(2026, 3, 10), 10.99m, BillState.Changed, 12.99m,
            new DateOnly(2026, 3, 8), [Guid.NewGuid()]);

        // Act
        bool followed = bill.FollowPrice(changed);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(followed).IsTrue();
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 3, 10))).IsEqualTo(10.99m);
            await Assert.That(bill.ExpectedOn(new DateOnly(2026, 4, 10))).IsEqualTo(12.99m);
        }
    }

    [Test]
    public async Task FollowPrice_PaidAsExpected_ChangesNothing()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10), amount: 10.99m);
        BillPeriod paid = new(new DateOnly(2026, 3, 10), 10.99m, BillState.Paid, 10.99m, new DateOnly(2026, 3, 10),
            [Guid.NewGuid()]);

        // Act & Assert
        await Assert.That(bill.FollowPrice(paid)).IsFalse();
    }

    [Test]
    public async Task EndThenReopen_IsDueAgain()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10));
        bill.End(new DateOnly(2026, 2, 1));

        // Act
        bool reopened = bill.Reopen();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reopened).IsTrue();
            await Assert.That(bill.HasEnded).IsFalse();
            await Assert.That(bill.NextDue(new DateOnly(2026, 6, 1))).IsEqualTo(new DateOnly(2026, 6, 10));
        }
    }

    [Test]
    public async Task EndByUser_ReopenFromASyncLeavesItEnded()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10));
        bill.EndByUser(new DateOnly(2026, 3, 1));

        // Act
        bool reopened = bill.Reopen();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reopened).IsFalse();
            await Assert.That(bill.EndedOn).IsEqualTo(new DateOnly(2026, 3, 1));
            await Assert.That(bill.EndedByUser).IsTrue();
        }
    }

    [Test]
    public async Task EndByUser_AlreadyEndedByTheBank_KeepsTheDayItEnded()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10));
        bill.End(new DateOnly(2026, 2, 1));

        // Act
        bool changed = bill.EndByUser(new DateOnly(2026, 3, 1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(bill.EndedOn).IsEqualTo(new DateOnly(2026, 2, 1));
            await Assert.That(bill.EndedByUser).IsTrue();
        }
    }

    [Test]
    public async Task ReopenByUser_EndedByTheUser_IsDueAgain()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10));
        bill.EndByUser(new DateOnly(2026, 3, 1));

        // Act
        bool reopened = bill.ReopenByUser();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(reopened).IsTrue();
            await Assert.That(bill.HasEnded).IsFalse();
            await Assert.That(bill.EndedByUser).IsFalse();
        }
    }

    [Test]
    public async Task End_AlreadyEnded_KeepsTheFirstDay()
    {
        // Arrange
        Bill bill = Build(BillFrequency.Monthly, new DateOnly(2026, 1, 10));
        bill.End(new DateOnly(2026, 2, 1));

        // Act
        bool ended = bill.End(new DateOnly(2026, 3, 1));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ended).IsFalse();
            await Assert.That(bill.EndedOn).IsEqualTo(new DateOnly(2026, 2, 1));
        }
    }

    private static Bill Build(BillFrequency frequency, DateOnly startsOn, BillSource source = BillSource.Manual,
        Guid? sourceId = null, decimal amount = 9.99m) =>
        new(UserId, AccountId, "Streaming", "Streamco", frequency, amount, startsOn, source, sourceId);
}
