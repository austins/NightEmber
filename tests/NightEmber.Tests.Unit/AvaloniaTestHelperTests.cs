using Avalonia;
using Avalonia.Threading;
using System.Collections.Concurrent;

namespace NightEmber.Tests.Unit;

public sealed class AvaloniaTestHelperTests
{
    [Fact]
    public async Task RunAsync_ConcurrentActions_RunOneAtATimeOnTheUiThreadWithSeparateApplications()
    {
        // Arrange
        const int actionCount = 4;
        var running = 0;
        var results = new ConcurrentQueue<(bool Overlapped, bool OnUiThread, Application? Application)>();

        // Act
        await Task.WhenAll(
            Enumerable
                .Range(0, actionCount)
                .Select(_ => AvaloniaTestHelper.RunAsync(() =>
                {
                    var overlapped = Interlocked.Increment(ref running) != 1;
                    results.Enqueue((overlapped, Dispatcher.UIThread.CheckAccess(), Application.Current));
                    Thread.Yield();
                    Interlocked.Decrement(ref running);
                })));

        // Assert
        results.Should().HaveCount(actionCount);
        results.Should().OnlyContain(result => !result.Overlapped && result.OnUiThread);
        results.Should().OnlyContain(result => result.Application != null);
        results.Should().OnlyHaveUniqueItems(result => result.Application);
    }

    [Fact]
    public async Task RunAsync_ActionThrows_PropagatesFailureAndAllowsNextAction()
    {
        // Arrange
        var expected = new InvalidOperationException("test failure");
        var nextActionRan = false;

        // Act
        var exception = await Record.ExceptionAsync(() => AvaloniaTestHelper.RunAsync(() => throw expected));
        await AvaloniaTestHelper
            .RunAsync(() => nextActionRan = true)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        exception.Should().BeSameAs(expected);
        nextActionRan.Should().BeTrue();
    }

    [Fact]
    public async Task FlushDispatcher_QueuedBackgroundWork_RunsBeforeReturning()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var ran = false;
            Dispatcher.UIThread.Post(() => ran = true, DispatcherPriority.Background);

            // Act
            AvaloniaTestHelper.FlushDispatcher();

            // Assert
            ran.Should().BeTrue();
        });
    }
}
