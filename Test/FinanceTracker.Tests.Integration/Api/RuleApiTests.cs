using System.Net;
using FinanceTracker.Contracts.Classifications;
using FinanceTracker.Contracts.Rules;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Contracts.Transactions;
using FinanceTracker.Tests.Integration.Http;
using FinanceTracker.Tests.Unit.Shared.Fakers;
using FinanceTracker.Tests.Unit.Shared.Scenarios;

namespace FinanceTracker.Tests.Integration.Api;

public class RuleApiTests : IntegrationTestBase
{
    [Test]
    public async Task AddRule_CanBeReadUpdatedReorderedAndDeleted()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        ApiSession api = ApiFor(user);
        IRulesService service = api.Get<IRulesService>();
        Classification shopping = await api.Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Shopping" }, CancellationToken);

        // Act
        Rule groceries = await service.AddRuleAsync(TagRule("Groceries", "GROCER", shopping), CancellationToken);
        Rule cafes = await service.AddRuleAsync(TagRule("Cafes", "CAFE", shopping), CancellationToken);

        SaveRule renamed = TagRule("Food shops", "GROCER", shopping);
        renamed.IsEnabled = false;
        await service.UpdateRuleAsync(groceries.Id, renamed, CancellationToken);
        await service.ReorderRulesAsync(new ReorderRules { RuleIds = [cafes.Id, groceries.Id] }, CancellationToken);
        List<Rule> ordered = await service.GetRulesAsync(CancellationToken).ToListAsync(CancellationToken);

        await service.DeleteRuleAsync(cafes.Id, CancellationToken);
        Rule remaining = (await service.GetRulesAsync(CancellationToken).ToListAsync(CancellationToken)).Single();

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(ordered.Select(static x => x.Name)).IsEquivalentTo(["Cafes", "Food shops"],
                TUnit.Assertions.Enums.CollectionOrdering.Matching);
            await Assert.That(remaining.Id).IsEqualTo(groceries.Id);
            await Assert.That(remaining.IsEnabled).IsFalse();
            await Assert.That(remaining.Conditions.Single().Value).IsEqualTo("GROCER");
            await Assert.That(remaining.Actions.Single().ClassificationId).IsEqualTo(shopping.ClassificationId);
        }
    }

    [Test]
    public async Task AddRule_Invalid_IsABadRequest()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        IRulesService service = ApiFor(user).Get<IRulesService>();

        // Act
        HttpRequestException? exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.AddRuleAsync(new SaveRule { Name = "Empty" }, CancellationToken));

        // Assert
        await Assert.That(exception!.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task AddRule_WithAnotherUsersTag_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        Classification theirs = await ApiFor(owner).Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Theirs" }, CancellationToken);

        IRulesService service = ApiFor(other).Get<IRulesService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.AddRuleAsync(TagRule("Borrowed", "GROCER", theirs), CancellationToken));
    }

    [Test]
    public async Task GetRule_OfAnotherUser_ThrowsAsTheServiceDoes()
    {
        // Arrange
        SignedInUser owner = await RegisterUserAsync();
        SignedInUser other = await RegisterUserAsync();
        ApiSession ownerApi = ApiFor(owner);
        Classification tag = await ownerApi.Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Mine" }, CancellationToken);
        Rule rule = await ownerApi.Get<IRulesService>().AddRuleAsync(TagRule("Mine", "GROCER", tag),
            CancellationToken);

        IRulesService service = ApiFor(other).Get<IRulesService>();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetRuleAsync(rule.Id, CancellationToken));
    }

    [Test]
    public async Task PreviewThenApply_TagsExistingTransactionsAndNamesTheRule()
    {
        // Arrange
        SignedInUser user = await RegisterUserAsync();
        await Scenario.Do(s => s.BankConnectionScenario.WithConnection("bank", user.Username))
            .Do(s => s.BankAccountScenario.WithAccount("main", "bank"))
            .Do(s => s.TransactionScenario.WithTransaction("grocer", "main",
                new TransactionDetailsFaker().Generate() with { Description = "GROCER STORE 1" }));

        ApiSession api = ApiFor(user);
        IRulesService service = api.Get<IRulesService>();
        Classification shopping = await api.Get<IClassificationService>()
            .AddCustomClassificationAsync(new AddClassifications { Tag = "Shopping" }, CancellationToken);
        Rule rule = await service.AddRuleAsync(TagRule("Groceries", "GROCER", shopping), CancellationToken);

        // Act
        RulePreview preview = await service.PreviewRuleAsync(rule.Id, TagRule("Groceries", "GROCER", shopping),
            CancellationToken);
        int applied = await service.ApplyRuleToExistingAsync(rule.Id, CancellationToken);
        Transaction tagged = (await api.Get<ITransactionsService>()
            .GetAllTransactionsAsync(new FilterTransactions(), CancellationToken)
            .ToListAsync(CancellationToken)).Single();
        SaveRule draft = await service.GetDraftFromTransactionAsync(tagged.TransactionId, CancellationToken);

        // Assert
        using (Assert.Multiple())
        {
            await Assert.That(preview.Changed).IsEqualTo(1);
            await Assert.That(applied).IsEqualTo(1);
            await Assert.That(tagged.TagsFromRules.Single().RuleName).IsEqualTo("Groceries");
            await Assert.That(draft.Actions.Single().ClassificationId).IsEqualTo(shopping.ClassificationId);
        }
    }

    private static SaveRule TagRule(string name, string contains, Classification tag) => new()
    {
        Name = name,
        Conditions = [new RuleCondition { Field = RuleField.Description, Operator = RuleOperator.Contains,
            Value = contains }],
        Actions = [new RuleAction { Type = RuleActionType.AddTag, ClassificationId = tag.ClassificationId }]
    };
}
