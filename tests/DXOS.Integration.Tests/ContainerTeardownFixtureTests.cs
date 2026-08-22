using DXOS.Integration.Tests.Teardown;
using Xunit;

namespace DXOS.Integration.Tests;

public sealed class ContainerTeardownFixtureTests
{
    [Fact]
    public async Task Teardown_WhenStopFails_PropagatesException_AndExecutesDisposal()
    {
        var disposed = false;
        var stopEx = new InvalidOperationException("Docker daemon stop error");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ContainerTeardownHelper.TeardownAsync(
                ct => Task.FromException(stopEx),
                () => { disposed = true; return ValueTask.CompletedTask; },
                stopTimeout: TimeSpan.FromSeconds(5),
                disposeTimeout: TimeSpan.FromSeconds(5));
        });

        Assert.True(disposed, "Disposal must still be executed even when StopAsync fails.");
        Assert.Contains("Testcontainers container StopAsync failed", ex.Message);
        Assert.Same(stopEx, ex.InnerException);
    }

    [Fact]
    public async Task Teardown_WhenDisposeFaults_PropagatesDisposalFault()
    {
        var disposeEx = new InvalidOperationException("Container disposal fault");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await ContainerTeardownHelper.TeardownAsync(
                ct => Task.CompletedTask,
                () => ValueTask.FromException(disposeEx),
                stopTimeout: TimeSpan.FromSeconds(5),
                disposeTimeout: TimeSpan.FromSeconds(5));
        });

        Assert.Same(disposeEx, ex);
    }

    [Fact]
    public async Task Teardown_WhenDisposeTimesOut_ThrowsTimeoutException()
    {
        var tcs = new TaskCompletionSource();

        var ex = await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await ContainerTeardownHelper.TeardownAsync(
                ct => Task.CompletedTask,
                () => new ValueTask(tcs.Task),
                stopTimeout: TimeSpan.FromSeconds(5),
                disposeTimeout: TimeSpan.FromMilliseconds(50));
        });

        Assert.Contains("disposal timed out", ex.Message);
        tcs.SetResult();
    }
}
