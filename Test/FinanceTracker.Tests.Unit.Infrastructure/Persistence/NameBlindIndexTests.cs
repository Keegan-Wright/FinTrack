using FinanceTracker.Infrastructure.Persistence;

namespace FinanceTracker.Tests.Unit.Infrastructure.Persistence;

public class NameBlindIndexTests
{
    [Test]
    [Arguments("Holiday", "name:HOLIDAY")]
    [Arguments("  holiday ", "name:HOLIDAY")]
    [Arguments("Car loan", "name:CAR LOAN")]
    public async Task Value_IgnoresCaseAndSpacesAtEitherEnd(string name, string expected) =>
        await Assert.That(NameBlindIndex.Value(name)).IsEqualTo(expected);

    [Test]
    public async Task Value_SeveralParts_JoinsThemWithASpace() =>
        await Assert.That(NameBlindIndex.Value(" Jane", "Doe ")).IsEqualTo("name:JANE DOE");
}
