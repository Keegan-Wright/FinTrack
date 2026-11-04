using Quartz;

namespace FinanceTracker.Tests.Unit.Infrastructure.Services.Automation;

/// <summary>
/// A job that runs until <see cref="Release"/>, so a test can catch it running.
/// </summary>
public sealed class HeldJob : IJob
{
    private static TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public static Task Started => _started.Task;

    public static void Reset()
    {
        _started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static void Release() => _release.TrySetResult();

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        _started.TrySetResult();
        await _release.Task.WaitAsync(cancellationToken);
    }
}
