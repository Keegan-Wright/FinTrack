using FinanceTracker.Contracts.Sharing;
using FinanceTracker.Contracts.Validation;

namespace FinanceTracker.Tests.Unit.Infrastructure.Validators;

public class ChangeAccountSharesValidatorTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private readonly ChangeAccountSharesValidator _validator = new();

    [Test]
    public async Task Validate_NoneChosen_Passes() =>
        await Assert.That(_validator.Validate(new ChangeAccountShares { Accounts = [] }).IsValid).IsTrue();

    [Test]
    public async Task Validate_AnAccountTwice_Fails()
    {
        ChangeAccountShares request = new()
        {
            Accounts =
            [
                new AccountShareChoice { AccountId = AccountId, Mode = ShareMode.ReadOnly },
                new AccountShareChoice { AccountId = AccountId, Mode = ShareMode.AllowChanges }
            ]
        };

        await Assert.That(_validator.Validate(request).Errors.Select(static x => x.ErrorMessage))
            .Contains("Each account can only be shared once");
    }

    [Test]
    public async Task Validate_AModeThatDoesntExist_Fails()
    {
        ChangeAccountShares request = new()
        {
            Accounts = [new AccountShareChoice { AccountId = AccountId, Mode = (ShareMode)42 }]
        };

        await Assert.That(_validator.Validate(request).IsValid).IsFalse();
    }
}
