using FinanceTracker.Domain.Bills;

namespace FinanceTracker.Tests.Unit.Domain.Bills;

[Category("Domain")]
public class StandingOrderFrequencyTests
{
    [Test]
    [Arguments("Monthly", BillFrequency.Monthly)]
    [Arguments("weekly", BillFrequency.Weekly)]
    [Arguments("Fortnightly", BillFrequency.Fortnightly)]
    [Arguments("Quarterly", BillFrequency.Quarterly)]
    [Arguments("Annual", BillFrequency.Yearly)]
    [Arguments("IntrvlWkDay:01:03", BillFrequency.Weekly)]
    [Arguments("IntrvlWkDay:02:05", BillFrequency.Fortnightly)]
    [Arguments("IntrvlWkDay:04:01", BillFrequency.FourWeekly)]
    [Arguments("IntrvlMnthDay:01:15", BillFrequency.Monthly)]
    [Arguments("IntrvlMnthDay:03:01", BillFrequency.Quarterly)]
    [Arguments("IntrvlMnthDay:12:28", BillFrequency.Yearly)]
    [Arguments("WkInMnthDay:02:03", BillFrequency.Monthly)]
    [Arguments("QtrDay:ENGLISH", BillFrequency.Quarterly)]
    public async Task Parse_KnownFrequency(string frequency, BillFrequency expected) =>
        await Assert.That(StandingOrderFrequency.Parse(frequency)).IsEqualTo(expected);

    [Test]
    [Arguments("EvryDay")]
    [Arguments("EvryWorkgDay")]
    [Arguments("IntrvlWkDay:03:01")]
    [Arguments("IntrvlMnthDay:02:01")]
    [Arguments("")]
    [Arguments(null)]
    public async Task Parse_OneABillCantFollow_IsNull(string? frequency) =>
        await Assert.That(StandingOrderFrequency.Parse(frequency)).IsNull();
}
