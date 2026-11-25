using System.Text.Json;
using Microsoft.Extensions.AI;

namespace FinanceTracker.Tests.Unit.Ai;

public class AiToolsTests : AiToolTestBase
{
    [Test]
    public async Task Functions_AreTheReadOnlyToolSet()
    {
        // Act
        List<string> names = Tools.Functions.Select(x => x.Name).Order().ToList();

        // Assert
        await Assert.That(names).IsEquivalentTo([
            "compare_periods", "get_bills", "get_budgets", "get_debts", "get_net_worth", "get_savings_goals",
            "get_spending_breakdown", "get_today", "get_totals", "get_upcoming_payments", "list_accounts", "list_tags",
            "search_transactions"
        ]);
    }

    [Test]
    public async Task Functions_EachHaveADescription()
    {
        // Act
        List<string> undescribed = Tools.Functions
            .Where(x => string.IsNullOrWhiteSpace(x.Description))
            .Select(x => x.Name)
            .ToList();

        // Assert
        await Assert.That(undescribed).IsEmpty();
    }

    [Test]
    public async Task Functions_DescribeEveryParameter()
    {
        // Arrange
        List<string> undescribed = [];

        // Act
        foreach (AIFunction function in Tools.Functions)
        {
            if (!function.JsonSchema.TryGetProperty("properties", out JsonElement properties))
            {
                continue;
            }

            foreach (JsonProperty parameter in properties.EnumerateObject())
            {
                if (!parameter.Value.TryGetProperty("description", out _))
                {
                    undescribed.Add($"{function.Name}.{parameter.Name}");
                }
            }
        }

        // Assert
        await Assert.That(undescribed).IsEmpty();
    }

    [Test]
    public async Task Functions_DoNotAskTheModelForServicesOrCancellation()
    {
        // Act
        List<string> parameters = Tools.Functions
            .Where(x => x.JsonSchema.TryGetProperty("properties", out _))
            .SelectMany(x => x.JsonSchema.GetProperty("properties").EnumerateObject().Select(p => p.Name))
            .ToList();

        // Assert
        await Assert.That(parameters).DoesNotContain("cancellationToken");
        await Assert.That(parameters).DoesNotContain("serviceProvider");
    }

    [Test]
    public async Task InvokeAsync_WithoutServices_Refuses()
    {
        // Arrange
        AIFunction tool = Tool("get_debts");

        // Act
        Func<Task> call = async () => await tool.InvokeAsync(new AIFunctionArguments());

        // Assert
        await Assert.That(call).Throws<InvalidOperationException>();
    }
}
