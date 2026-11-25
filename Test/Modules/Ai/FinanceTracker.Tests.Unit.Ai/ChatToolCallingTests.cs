using System.Text.Json;
using FinanceTracker.Contracts.Debts;
using FinanceTracker.Contracts.Services;
using FinanceTracker.Tests.Unit.Shared;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Ai;

public class ChatToolCallingTests : AiToolTestBase
{
    [Test]
    public async Task GetResponseAsync_WhenTheModelCallsATool_HandsItTheResult()
    {
        // Arrange
        IDebtsService debtsService = Substitute.For<IDebtsService>();
        debtsService.GetDebtsAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { Debt("Car loan", 4200m) }.ToAsyncEnumerable());
        Services.AddSingleton(debtsService);
        await using ServiceProvider provider = Services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();

        ScriptedChatClient model = new(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_debts")])),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "You owe 4,200 on the car loan.")));
        using IChatClient chat = new ChatClientBuilder(model)
            .UseFunctionInvocation()
            .Build(scope.ServiceProvider);
        ChatOptions options = new() { Tools = [.. Tools.Functions] };

        // Act
        ChatResponse response = await chat.GetResponseAsync("How much do I owe?", options);

        // Assert
        FunctionResultContent result = model.Requests[1]
            .SelectMany(x => x.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        await Assert.That(result.CallId).IsEqualTo("call-1");
        await Assert.That(result.Result?.ToString()).Contains("Car loan");
        await Assert.That(response.Text).IsEqualTo("You owe 4,200 on the car loan.");
    }

    [Test]
    public async Task GetResponseAsync_ModelAsksForToday_GetsTheWeekStartToWorkFrom()
    {
        // Arrange
        using CultureScope culture = new("en-GB");
        await using ServiceProvider provider = Services.BuildServiceProvider();
        await using AsyncServiceScope scope = provider.CreateAsyncScope();
        ScriptedChatClient model = new(
            new ChatResponse(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "get_today")])),
            new ChatResponse(new ChatMessage(ChatRole.Assistant, "This week started on Monday.")));
        using IChatClient chat = new ChatClientBuilder(model)
            .UseFunctionInvocation()
            .Build(scope.ServiceProvider);
        ChatOptions options = new() { Tools = [.. Tools.Functions] };

        // Act
        await chat.GetResponseAsync("How much have I spent this week?", options);

        // Assert, today is Monday 2026-06-15
        FunctionResultContent result = model.Requests[1]
            .SelectMany(x => x.Contents)
            .OfType<FunctionResultContent>()
            .Single();
        using JsonDocument today = JsonDocument.Parse(result.Result!.ToString()!);
        await Assert.That(today.RootElement.GetProperty("weekStart").GetString()).IsEqualTo("2026-06-15");
    }

    private static Debt Debt(string name, decimal owed) =>
        new() { Name = name, Owed = owed, Forecast = new DebtForecast() };
}
