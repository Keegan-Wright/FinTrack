using FinanceTracker.Domain.Budgeting;
using FinanceTracker.Domain.Common;

namespace FinanceTracker.Tests.Unit.Domain.Budgeting;

[Category("Domain")]
public class BudgetTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid Shopping = Guid.NewGuid();
    private static readonly Guid Joint = Guid.NewGuid();
    private static readonly Guid Main = Guid.NewGuid();
    private static readonly BudgetMonth January = new(2026, 1);
    private static readonly BudgetMonth February = new(2026, 2);
    private static readonly BudgetMonth March = new(2026, 3);

    [Test]
    public async Task ChangeLimit_FromAMonth_LeavesTheMonthsBefore()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act
        bool changed = budget.ChangeLimit(500m, March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsTrue();
            await Assert.That(budget.LimitFor(January)).IsEqualTo(400m);
            await Assert.That(budget.LimitFor(February)).IsEqualTo(400m);
            await Assert.That(budget.LimitFor(March)).IsEqualTo(500m);
            await Assert.That(budget.LimitFor(new BudgetMonth(2027, 6))).IsEqualTo(500m);
        }
    }

    [Test]
    public async Task ChangeLimit_BeforeALaterChange_ReplacesIt()
    {
        // Arrange
        Budget budget = Groceries(400m);
        budget.ChangeLimit(500m, March);

        // Act
        budget.ChangeLimit(450m, February);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(budget.LimitFor(March)).IsEqualTo(450m);
            await Assert.That(budget.Limits).Count().IsEqualTo(2);
        }
    }

    [Test]
    public async Task ChangeLimit_ToWhatItAlreadyIs_ChangesNothing()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act
        bool changed = budget.ChangeLimit(400m, March);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(changed).IsFalse();
            await Assert.That(budget.Limits).Count().IsEqualTo(1);
        }
    }

    [Test]
    public async Task LimitFor_BeforeTheFirstMonth_IsNull() =>
        await Assert.That(Groceries(400m).LimitFor(new BudgetMonth(2025, 12))).IsNull();

    [Test]
    public async Task RemoveTag_ItsLastTag_KeepsTheBudgetCoveringNothing()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act
        bool removed = budget.RemoveTag(Shopping);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(budget.CoversNothing).IsTrue();
            await Assert.That(budget.LimitFor(January)).IsEqualTo(400m);
        }
    }

    [Test]
    public void New_NoTagsOrAccounts_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            _ = new Budget(UserId, "Groceries", [], [], 400m, January, RolloverMode.None));

    [Test]
    public void ChangeLimit_Negative_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Groceries(400m).ChangeLimit(-1m, March));

    [Test]
    public void ChangeRolloverMode_Unknown_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Groceries(400m).ChangeRolloverMode((RolloverMode)9));

    private static Budget Groceries(decimal limit) =>
        new(UserId, "Groceries", [Shopping], [], limit, January, RolloverMode.None);

    [Test]
    public async Task ReplaceCoverage_FromItsChoices_CoversThem()
    {
        Guid groceries = Guid.NewGuid();
        Guid household = Guid.NewGuid();
        Budget budget = new(UserId, "Food", [groceries], [], 400m, January, RolloverMode.None);

        budget.ReplaceCoverage([household, groceries], new HashSet<Guid> { groceries, household }, [],
            new HashSet<Guid>());

        await Assert.That(budget.TagIds).IsEquivalentTo([household, groceries]);
    }

    [Test]
    public async Task ReplaceCoverage_ATagNotAChoice_IsRefusedAndKeepsItsTags()
    {
        Guid groceries = Guid.NewGuid();
        Budget budget = new(UserId, "Food", [groceries], [], 400m, January, RolloverMode.None);

        using (Assert.Multiple())
        {
            await Assert.That(() => budget.ReplaceCoverage([Guid.NewGuid()], new HashSet<Guid> { groceries }, [],
                    new HashSet<Guid>()))
                .Throws<DomainRuleException>();
            await Assert.That(budget.TagIds).IsEquivalentTo([groceries]);
        }
    }

    [Test]
    public async Task New_AccountsWithoutTags_CoversTheAccounts()
    {
        // Act
        Budget budget = new(UserId, "Joint account", [], [Joint], 1500m, January, RolloverMode.None);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(budget.AccountIds).IsEquivalentTo([Joint]);
            await Assert.That(budget.TagIds).IsEmpty();
            await Assert.That(budget.CoversNothing).IsFalse();
        }
    }

    [Test]
    public async Task RemoveAccount_ItsLastWithNoTags_KeepsTheBudgetCoveringNothing()
    {
        // Arrange
        Budget budget = new(UserId, "Joint account", [], [Joint], 1500m, January, RolloverMode.None);

        // Act
        bool removed = budget.RemoveAccount(Joint);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(removed).IsTrue();
            await Assert.That(budget.CoversNothing).IsTrue();
        }
    }

    [Test]
    public async Task RemoveAccount_ItsLastWithTags_StillCoversTheTags()
    {
        // Arrange
        Budget budget = new(UserId, "Joint groceries", [Shopping], [Joint], 400m, January, RolloverMode.None);

        // Act
        budget.RemoveAccount(Joint);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(budget.AccountIds).IsEmpty();
            await Assert.That(budget.CoversNothing).IsFalse();
        }
    }

    [Test]
    public async Task MoveAccount_AMergedCopy_CoversTheAccountItWasMergedInto()
    {
        // Arrange
        Guid theirCopy = Guid.NewGuid();
        Budget budget = new(UserId, "Joint account", [], [theirCopy, Main], 1500m, January, RolloverMode.None);

        // Act
        bool moved = budget.MoveAccount(theirCopy, Joint);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(moved).IsTrue();
            await Assert.That(budget.AccountIds).IsEquivalentTo([Main, Joint]);
        }
    }

    [Test]
    public async Task MoveAccount_IntoAnAccountItAlreadyCovers_ListsItOnce()
    {
        // Arrange
        Guid theirCopy = Guid.NewGuid();
        Budget budget = new(UserId, "Joint account", [], [theirCopy, Joint], 1500m, January, RolloverMode.None);

        // Act
        budget.MoveAccount(theirCopy, Joint);

        // Assert
        await Assert.That(budget.AccountIds).IsEquivalentTo([Joint]);
    }

    [Test]
    public async Task MoveAccount_OneItDoesntCover_ChangesNothing()
    {
        // Arrange
        Budget budget = new(UserId, "Joint account", [], [Joint], 1500m, January, RolloverMode.None);

        // Act
        bool moved = budget.MoveAccount(Guid.NewGuid(), Main);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(moved).IsFalse();
            await Assert.That(budget.AccountIds).IsEquivalentTo([Joint]);
        }
    }

    [Test]
    public async Task ReplaceCoverage_FromTagsToAnAccount_CoversTheAccountOnly()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act
        budget.ReplaceCoverage([], new HashSet<Guid> { Shopping }, [Joint], new HashSet<Guid> { Joint });

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(budget.TagIds).IsEmpty();
            await Assert.That(budget.AccountIds).IsEquivalentTo([Joint]);
        }
    }

    [Test]
    public async Task ReplaceCoverage_AnAccountNotAChoice_IsRefusedAndKeepsWhatItCovers()
    {
        // Arrange
        Budget budget = Groceries(400m);

        // Act & Assert
        using (Assert.Multiple())
        {
            await Assert.That(() => budget.ReplaceCoverage([Shopping], new HashSet<Guid> { Shopping }, [Joint],
                    new HashSet<Guid> { Main }))
                .Throws<DomainRuleException>();
            await Assert.That(budget.TagIds).IsEquivalentTo([Shopping]);
            await Assert.That(budget.AccountIds).IsEmpty();
        }
    }

    [Test]
    public void ReplaceCoverage_NoTagsOrAccounts_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            Groceries(400m).ReplaceCoverage([], new HashSet<Guid>(), [], new HashSet<Guid>()));
}
