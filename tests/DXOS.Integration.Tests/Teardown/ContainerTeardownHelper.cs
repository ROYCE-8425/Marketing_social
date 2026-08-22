namespace DXOS.Integration.Tests.Teardown;

public static class ContainerTeardownHelper
{
    public static async ValueTask TeardownAsync(
        Func<CancellationToken, Task> stopAsync,
        Func<ValueTask> disposeAsync,
        TimeSpan? stopTimeout = null,
        TimeSpan? disposeTimeout = null)
    {
        var actualStopTimeout = stopTimeout ?? TimeSpan.FromSeconds(30);
        var actualDisposeTimeout = disposeTimeout ?? TimeSpan.FromSeconds(15);

        using var cts = new CancellationTokenSource(actualStopTimeout);
        Exception? stopException = null;
        try
        {
            await stopAsync(cts.Token);
        }
        catch (Exception ex)
        {
            stopException = ex;
            Console.WriteLine($"[DXOS_INTEGRATION_TESTCONTAINER_STOP_ERROR] {ex.Message}");
        }

        using var disposeCts = new CancellationTokenSource(actualDisposeTimeout);
        var disposeTask = disposeAsync().AsTask();
        var timeoutTask = Task.Delay(actualDisposeTimeout, disposeCts.Token);
        var completedTask = await Task.WhenAny(disposeTask, timeoutTask);
        if (completedTask != disposeTask)
        {
            Console.WriteLine("[DXOS_INTEGRATION_TESTCONTAINER_DISPOSE_TIMEOUT]");
            throw new TimeoutException($"Testcontainers container disposal timed out after {actualDisposeTimeout.TotalSeconds}s.");
        }

        disposeCts.Cancel();

        // Await disposeTask to observe and propagate any disposal faults
        await disposeTask;

        if (stopException != null)
        {
            throw new InvalidOperationException($"Testcontainers container StopAsync failed: {stopException.Message}", stopException);
        }
    }
}
