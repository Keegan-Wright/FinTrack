using FinanceTracker.Client.Components.Bills;
using FinanceTracker.Contracts.Bills;

namespace FinanceTracker.Tests.Unit.Client.Bills;

public class BillTextTests
{
    private static readonly DateOnly Today = new(2026, 10, 30);

    [Test]
    [Arguments(BillState.Paid, "Paid 12 Oct")]
    [Arguments(BillState.Upcoming, "Due 12 Oct")]
    [Arguments(BillState.Late, "Late, due 12 Oct")]
    [Arguments(BillState.Changed, "Paid 12 Oct, price changed")]
    public async Task PeriodLabel_SaysHowItStands(BillState state, string expected)
    {
        // Arrange
        using CultureScope culture = new();
        BillPeriod period = new()
        {
            Due = new DateOnly(2026, 10, 12), State = state, Expected = 9.99m,
            PaidOn = state is BillState.Paid or BillState.Changed ? new DateOnly(2026, 10, 12) : null
        };

        // Act & Assert
        await Assert.That(BillText.PeriodLabel(period, Today)).IsEqualTo(expected);
    }

    [Test]
    public async Task Changed_SaysWhatWasPaidAgainstWhatWasExpected()
    {
        // Arrange
        using CultureScope culture = new();
        BillPeriod period = new() { Due = Today, State = BillState.Changed, Expected = 9.99m, Paid = 12.99m };

        // Act & Assert
        await Assert.That(BillText.Changed(period)).IsEqualTo("Paid £12.99, expected £9.99");
    }

    [Test]
    public async Task Dates_NextDueAndLastPaid()
    {
        // Arrange
        using CultureScope culture = new();
        Bill bill = Bill();

        // Act & Assert
        await Assert.That(BillText.Dates(bill, Today)).IsEqualTo("Next due 12 Nov · Last paid £9.99 on 12 Oct");
    }

    [Test]
    public async Task Dates_EndedAndNeverPaid()
    {
        // Arrange
        using CultureScope culture = new();
        Bill bill = Bill(endedOn: new DateOnly(2026, 10, 30), paid: false);

        // Act & Assert
        await Assert.That(BillText.Dates(bill, Today)).IsEqualTo("Ended 30 Oct · Not paid yet");
    }

    [Test]
    public async Task Summary_AccountFrequencyAndSource() =>
        await Assert.That(BillText.Summary(Bill())).IsEqualTo("Current · Monthly · Direct debit");

    [Test]
    public async Task Day_AnotherYear_IncludesIt()
    {
        using CultureScope culture = new();
        await Assert.That(BillText.Day(new DateOnly(2027, 1, 3), Today)).IsEqualTo("3 Jan 2027");
    }

    [Test]
    public async Task PickerLabel_TellsApartBillsWithTheSameName()
    {
        // Arrange
        using CultureScope culture = new();

        // Act
        string running = BillText.PickerLabel(Bill("Phone"));
        string ended = BillText.PickerLabel(Bill("Phone", endedOn: Today));

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(running).IsEqualTo("Phone · Current · £9.99 monthly");
            await Assert.That(ended).IsEqualTo("Phone · Current · £9.99 monthly · ended");
        }
    }

    internal static Bill Bill(string name = "Energy", DateOnly? endedOn = null, bool paid = true,
        BillSource source = BillSource.DirectDebit) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Payee = name.ToUpperInvariant(),
        AccountId = Guid.NewGuid(),
        AccountName = "Current",
        Source = source,
        Frequency = BillFrequency.Monthly,
        StartsOn = new DateOnly(2026, 1, 12),
        NextDue = endedOn is null ? new DateOnly(2026, 11, 12) : null,
        EndedOn = endedOn,
        ExpectedAmount = 9.99m,
        TolerancePercent = 5,
        MonthlyCost = endedOn is null ? 9.99m : 0,
        LastPaid = paid ? 9.99m : null,
        LastPaidOn = paid ? new DateOnly(2026, 10, 12) : null,
        FollowsTheBank = source == BillSource.StandingOrder,
        CanDelete = source is BillSource.Manual or BillSource.Detected
    };
}
