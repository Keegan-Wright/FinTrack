using FinanceTracker.Domain.OpenBanking.Accounts;

namespace FinanceTracker.Tests.Unit.Domain.OpenBanking.Accounts;

[Category("Domain")]
public class AccountIdentifierTests
{
    private const string UkIban = "GB29NWBK12345612345678";

    [Test]
    [Arguments("12-34-56", "1234 5678")]
    [Arguments("123456", "12345678")]
    public async Task AccountIdentifier_Create_KeepsOnlyTheDigits(string sortCode, string number)
    {
        AccountIdentifier? identifier = AccountIdentifier.Create(sortCode, number, null);

        using (Assert.Multiple())
        {
            await Assert.That(identifier!.SortCode).IsEqualTo("123456");
            await Assert.That(identifier.Number).IsEqualTo("12345678");
            await Assert.That(identifier.Iban).IsNull();
        }
    }

    [Test]
    public async Task AccountIdentifier_Create_NormalisesTheIban() =>
        await Assert.That(AccountIdentifier.Create(null, null, "de89 3704 0044 0532 0130 00")!.Iban)
            .IsEqualTo("DE89370400440532013000");

    [Test]
    public async Task AccountIdentifier_Create_UkIbanOnly_ReadsTheSortCodeAndNumberFromIt()
    {
        AccountIdentifier? identifier = AccountIdentifier.Create(null, null, UkIban);

        using (Assert.Multiple())
        {
            await Assert.That(identifier!.SortCode).IsEqualTo("123456");
            await Assert.That(identifier.Number).IsEqualTo("12345678");
            await Assert.That(identifier.Iban).IsEqualTo(UkIban);
        }
    }

    [Test]
    public async Task AccountIdentifier_Create_NonUkIban_HasNoSortCode() =>
        await Assert.That(AccountIdentifier.Create(null, null, "DE89370400440532013000")!.SortCode).IsNull();

    [Test]
    public async Task AccountIdentifier_Create_SortCodeWithoutNumber_KeepsOnlyTheIban()
    {
        AccountIdentifier? identifier = AccountIdentifier.Create("123456", null, "DE89370400440532013000");

        using (Assert.Multiple())
        {
            await Assert.That(identifier!.SortCode).IsNull();
            await Assert.That(identifier.Number).IsNull();
        }
    }

    [Test]
    [Arguments(null, null, null)]
    [Arguments(null, "12345678", null)]
    [Arguments("123456", null, "")]
    [Arguments("12-34-56", "  ", " ")]
    public async Task AccountIdentifier_Create_WithNeitherForm_IsNull(string? sortCode, string? number, string? iban) =>
        await Assert.That(AccountIdentifier.Create(sortCode, number, iban)).IsNull();

    [Test]
    [Arguments("123456 12345678")]
    [Arguments("12-34-56 12345678")]
    [Arguments("TO 123456 12345678 REF")]
    public async Task AccountIdentifier_IsNamedIn_ReferenceWithTheSortCodeAndNumber_IsTrue(string reference) =>
        await Assert.That(AccountIdentifier.Create("123456", "12345678", null)!.IsNamedIn(reference)).IsTrue();

    [Test]
    [Arguments("DE89 3704 0044 0532 0130 00")]
    [Arguments("from de89370400440532013000")]
    public async Task AccountIdentifier_IsNamedIn_ReferenceWithTheIban_IsTrue(string reference) =>
        await Assert.That(AccountIdentifier.Create(null, null, "DE89370400440532013000")!.IsNamedIn(reference))
            .IsTrue();

    [Test]
    [Arguments("123456 12345678")]
    [Arguments("GB29 NWBK 1234 5612 3456 78")]
    public async Task AccountIdentifier_IsNamedIn_UkIbanOnly_MatchesEitherForm(string reference) =>
        await Assert.That(AccountIdentifier.Create(null, null, UkIban)!.IsNamedIn(reference)).IsTrue();

    [Test]
    [Arguments("123456 87654321")]
    [Arguments("654321 12345678")]
    [Arguments("036112481/2")]
    [Arguments("GB29NWBK12345687654321")]
    [Arguments("")]
    [Arguments(null)]
    public async Task AccountIdentifier_IsNamedIn_AnyOtherReference_IsFalse(string? reference) =>
        await Assert.That(AccountIdentifier.Create("123456", "12345678", UkIban)!.IsNamedIn(reference)).IsFalse();
}
