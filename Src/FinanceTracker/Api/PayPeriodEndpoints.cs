using FinanceTracker.Contracts.Api;
using FinanceTracker.Contracts.PayPeriods;
using FinanceTracker.Contracts.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FinanceTracker.Api;

public static class PayPeriodEndpoints
{
    extension(RouteGroupBuilder api)
    {
        /// <summary>
        /// <see cref="IPayPeriodsService"/> at <see cref="ApiRoutes.PayPeriods"/>. Choosing an income that isn't
        /// coming in like pay, or turning pay periods on before a pay is found, answers 404.
        /// </summary>
        public RouteGroupBuilder MapPayPeriodEndpoints()
        {
            RouteGroupBuilder payPeriods = api.MapGroup(ApiRoutes.PayPeriods);

            payPeriods.MapGet(string.Empty, async Task<Ok<YourPay>> (IPayPeriodsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.GetYourPayAsync(cancellationToken)));

            payPeriods.MapGet("incomes", (IPayPeriodsService service, CancellationToken cancellationToken) =>
                service.GetIncomesAsync(cancellationToken));

            payPeriods.MapPut("pay", async Task<Ok<YourPay>> (ChooseIncome request, IPayPeriodsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.ChooseAsync(request, cancellationToken)));

            payPeriods.MapPut("off", async Task<Ok<YourPay>> (IPayPeriodsService service,
                    CancellationToken cancellationToken) =>
                TypedResults.Ok(await service.TurnOffAsync(cancellationToken)));

            payPeriods.MapPut("on", async Task<Ok<YourPay>> (IPayPeriodsService service,
                        CancellationToken cancellationToken) =>
                    TypedResults.Ok(await service.TurnOnAsync(cancellationToken)));

            return api;
        }
    }
}
