using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.Sequential;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.Ollama;

namespace FinanceTracker.Ai.Services;

public class ChatService : IChatService
{
    private readonly Kernel _kernel;
    public ChatService(Kernel kernel)
    {
        _kernel = kernel;
    }

    [Experimental("SKEXP0110")]
    public async Task Chat()
    {
        var financialAgent = new ChatCompletionAgent()
        {
            Id = "FinancialAgent",
            Description = "Provides financial input",
            InstructionsRole = AuthorRole.Assistant,
            Instructions = """
                           You are a financial analyst, You can summarise financial data providing accurate readings of transactions, amounts, time frames.
                           Do NOT give advice.
                           """,
            Kernel = _kernel,
            Arguments = new KernelArguments(
                new OllamaPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                })
        };

        var formattingAgent = new ChatCompletionAgent()
        {
            Id = "OutputFormatter",
            Description = "Responsible for outputting the final results to the user",
            InstructionsRole = AuthorRole.Assistant,
            Instructions = $"""
                            You are an output formatter. Having being given relevant information to the task you should ensure consistent tone, improve clarity,
                            polish your response. Output the final result in mark down, any culture should match {CultureInfo.CurrentCulture}
                            """,
            Kernel = _kernel
        };

        // Create a monitor to capturing agent responses (via ResponseCallback)
        // to display at the end of this sample. (optional)
        // NOTE: Create your own callback to capture responses in your application or service.
        OrchestrationMonitor monitor = new();
        // Define the orchestration
        SequentialOrchestration orchestration =
            new(financialAgent, formattingAgent)
            {
                ResponseCallback = monitor.ResponseCallback,
                StreamingResponseCallback =  monitor.StreamingResultCallback,
            };

        // Start the runtime
        InProcessRuntime runtime = new();
        await runtime.StartAsync();

        // Run the orchestration
        string input = "What are my 4 latest transactions?";
        Console.WriteLine($"\n# INPUT: {input}\n");
        OrchestrationResult<string> result = await orchestration.InvokeAsync(input, runtime);
        string text = await result.GetValueAsync(TimeSpan.FromSeconds(60));
        Console.WriteLine($"\n# RESULT: {text}");

        await runtime.RunUntilIdleAsync();

        Console.WriteLine("\n\nORCHESTRATION HISTORY");
        foreach (ChatMessageContent message in monitor.History)
        {
            var a = 1;
        }

    }
    protected sealed class OrchestrationMonitor
    {
        public List<StreamingChatMessageContent> StreamedResponses = [];

        public ChatHistory History { get; } = [];

        public ValueTask ResponseCallback(ChatMessageContent response)
        {
            this.History.Add(response);
            //WriteResponse(response);
            return ValueTask.CompletedTask;
        }

        public ValueTask StreamingResultCallback(StreamingChatMessageContent streamedResponse, bool isFinal)
        {
            this.StreamedResponses.Add(streamedResponse);

            if (isFinal)
            {
                this.StreamedResponses.Add(streamedResponse);
                //WriteStreamedResponse(this.StreamedResponses);
                //this.StreamedResponses.Clear();
            }

            return ValueTask.CompletedTask;
        }
    }
}

