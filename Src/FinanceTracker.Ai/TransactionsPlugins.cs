// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ComponentModel;
using System.Security.Claims;
using FinanceTracker.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace FinanceTracker.Ai;

public class TransactionsPlugins
{
    private readonly IDbContextFactory<FinanceTrackerContext> _contextFactory;
    private readonly Guid _userId;

    public TransactionsPlugins(IDbContextFactory<FinanceTrackerContext> contextFactory, ClaimsPrincipal claimsPrincipal)
    {
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
