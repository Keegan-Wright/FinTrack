using FinanceTracker.Client.Components.Reports;

namespace FinanceTracker.Tests.Unit.Client.Reports;

public class ReportNodeTests
{
    private static readonly Guid Current = Guid.NewGuid();

    [Test]
    [Arguments("Groceries", "Groceries", true)]
    [Arguments("Groceries", "Household", false)]
    public async Task IsSameAs_ComparesTheTitle(string title, string other, bool expected) =>
        await Assert.That(Node(title).IsSameAs(Node(other))).IsEqualTo(expected);

    [Test]
    public async Task IsSameAs_AccountReports_AlsoCompareTheAccount() =>
        await Assert.That(Node("Current", Current).IsSameAs(Node("Current", Guid.NewGuid()))).IsFalse();

    [Test]
    public async Task ChildLike_FindsTheSamePeriodOrNothing()
    {
        // Arrange
        ReportNode refreshed = new()
        {
            Label = "2026", Title = "2026", Children = [Node("October 2026"), Node("November 2026")]
        };

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(refreshed.ChildLike(Node("October 2026"))?.Title).IsEqualTo("October 2026");
            await Assert.That(refreshed.ChildLike(Node("December 2026"))).IsNull();
        }
    }

    private static ReportNode Node(string title, Guid? accountId = null) =>
        new() { Label = title, Title = title, AccountId = accountId };
}
