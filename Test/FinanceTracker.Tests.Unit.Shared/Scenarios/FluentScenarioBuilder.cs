namespace FinanceTracker.Tests.Unit.Shared.Scenarios;

public static class FluentScenarioBuilder
{
    public static async Task<TResult>
        Do<TSource, TResult>(this Task<TSource> source, Func<TSource, Task<TResult>> func) => await func(await source);

    public static async Task<TResult>
        Do<TSource, TResult>(this TSource source, Func<TSource, Task<TResult>> func) => await func(source);


    public static async Task<TResult>
        Do<TSource, TResult>(this Task<TSource> source, Func<TSource, TResult> func) => func(await source);

}
