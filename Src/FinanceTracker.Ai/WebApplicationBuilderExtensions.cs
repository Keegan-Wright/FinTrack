using System.ComponentModel;
using System.Security.Claims;
using FinanceTracker.Configurations;
using FinanceTracker.Data;
using FinanceTracker.Security.Encryption;
using FinanceTracker.Services.Dashboard;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel;

namespace FinanceTracker.Ai;

public static class WebApplicationBuilderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddAiModule()
        {


            builder.AddOllamaApiClient("chat")
                .AddKeyedChatClient("chat");
            builder.AddOllamaApiClient("embeddings")
                .AddKeyedEmbeddingGenerator("embeddings");


            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddOllamaChatCompletion("llama3.2", new Uri(builder.Configuration["OLLAMA_LLAMA3_2_URI"]));
            kernelBuilder.AddOllamaEmbeddingGenerator("llama3.2", new Uri(builder.Configuration["OLLAMA_LLAMA3_2_URI"]));

            kernelBuilder.Services.AddScoped<ISymmetricEncryptionService, SymmetricEncryptionService>();
            kernelBuilder.Services.AddHttpContextAccessor();
            kernelBuilder.Services.AddScoped<ClaimsPrincipal?>(s =>
                s.GetService<IHttpContextAccessor>()?.HttpContext?.User ?? null);

            kernelBuilder.Services.AddDbContextFactory<FinanceTrackerContext>((sp, options) =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("FinTrackDb"), npgsqlDbContextOptionsBuilder =>
                {
                    npgsqlDbContextOptionsBuilder.MigrationsAssembly("FinanceTracker.Data.Migrations");
                    npgsqlDbContextOptionsBuilder.EnableRetryOnFailure();
                    npgsqlDbContextOptionsBuilder.CommandTimeout(0);
                });
            });

            EncryptionConfiguration encryptionConfig = new()
            {
                SymmetricKey = builder.Configuration.GetValue<string>("ENCRYPTION_KEY")!,
                SymmetricSalt = builder.Configuration.GetValue<string>("ENCRYPTION_SALT")!,
                Iterations = builder.Configuration.GetValue<int>("ENCRYPTION_ITERATIONS")
            };

            kernelBuilder.Services.AddSingleton(encryptionConfig);

            kernelBuilder.Plugins.AddFromType<HowAreYouTodayPlugin>("Moods");
            kernelBuilder.Plugins.AddFromType<TransactionsPlugins>("Transactions");


            builder.Services.AddScoped<Kernel>(_ => kernelBuilder.Build());

            return builder;
        }
    }
}

public class HowAreYouTodayPlugin
{

    private readonly List<string> moods = new()
    {
        "Happy",
        "Sad",
        "Angry",
        "Fine"
    };

    [KernelFunction]
    [Description("Determines how you the AI model are feeling today")]
    public async Task<String> HowAreYouToday()
    {
        var num = Random.Shared.Next(moods.Count);

        return moods[num];
    }
}


public class TransactionsPlugins
{
    private readonly IDbContextFactory<FinanceTrackerContext> _contextFactory;
    private readonly Guid _userId;

    public TransactionsPlugins(IDbContextFactory<FinanceTrackerContext> contextFactory, ClaimsPrincipal claimsPrincipal)
    {
        var a = 1;
        _contextFactory = contextFactory;
         _userId = Guid.Parse(claimsPrincipal.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value!);
    }

    [KernelFunction]
    [Description("Fetches the users 10 most recent transactions")]
    public async Task<IEnumerable<AITransactionReponse>> HowAreYouToday()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var query = context.IsolateToUser(_userId)
                .Include(x => x.Providers)
                .ThenInclude(x => x.Transactions)
                .SelectMany(x => x.Providers.SelectMany(c => c.Transactions))
                .OrderByDescending(x => x.Created)
                .Take(10)
                .Select(x => new AITransactionReponse()
                {
                    Name = x.Description,
                    Amount = x.Amount
                });

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            var a = 1;
            return [];
        }

    }

    public class AITransactionReponse
    {
        public string Name { get; set; }
        public decimal Amount { get; set; }
    }

}
