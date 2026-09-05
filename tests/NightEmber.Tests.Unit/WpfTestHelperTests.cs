using System.Collections.Concurrent;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace NightEmber.Tests.Unit;

public sealed class WpfTestHelperTests
{
    [Fact]
    public async Task RunAsync_ConcurrentActions_KeepSeparateStaDispatchersAndSerializedCleanup()
    {
        // Arrange
        const int actionCount = 4;
        var startedActions = 0;
        var completedCleanups = 0;
        var results =
            new ConcurrentQueue<(Thread Thread, Dispatcher Dispatcher, ApartmentState Apartment, bool
                PreviousCleanupFinished)>();

        // Act
        await Task.WhenAll(
            Enumerable
                .Range(0, actionCount)
                .Select(_ => WpfTestHelper.RunAsync(() =>
                {
                    var position = Interlocked.Increment(ref startedActions);
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    dispatcher.ShutdownFinished += (_, _) => Interlocked.Increment(ref completedCleanups);
                    results.Enqueue(
                        (Thread.CurrentThread, dispatcher, Thread.CurrentThread.GetApartmentState(),
                            Volatile.Read(ref completedCleanups) == position - 1));
                })));

        // Assert
        results.Should().HaveCount(actionCount);
        results.Select(result => result.Thread).Should().OnlyHaveUniqueItems();
        results.Select(result => result.Dispatcher).Should().OnlyHaveUniqueItems();
        results.Should().OnlyContain(result => result.Apartment == ApartmentState.STA);
        results.Should().OnlyContain(result => result.PreviousCleanupFinished);
        results.Should().OnlyContain(result => result.Dispatcher.HasShutdownFinished);
        completedCleanups.Should().Be(actionCount);
    }

    [Fact]
    public async Task RunAsync_CleanupInProgress_DoesNotCompleteOrRunTheNextAction()
    {
        // Arrange
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowCleanup = new ManualResetEventSlim();
        var cleanupFinished = false;
        var nextActionSawCleanupFinished = false;
        var first = WpfTestHelper.RunAsync(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcher.ShutdownStarted += (_, _) =>
            {
                cleanupStarted.SetResult();
                // Hold cleanup at a known point; the timeout only bounds a failed test.
                if (!allowCleanup.Wait(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("The test did not release dispatcher cleanup.");
                }
            };
            dispatcher.ShutdownFinished += (_, _) => Volatile.Write(ref cleanupFinished, true);
        });
        var tasks = new List<Task> { first };
        bool completedDuringCleanup;

        // Act
        try
        {
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
            completedDuringCleanup = first.IsCompleted;
            tasks.Add(WpfTestHelper.RunAsync(() => nextActionSawCleanupFinished = Volatile.Read(ref cleanupFinished)));
        }
        finally
        {
            allowCleanup.Set();
            await Task.WhenAll(tasks);
        }

        // Assert
        completedDuringCleanup.Should().BeFalse();
        cleanupFinished.Should().BeTrue();
        nextActionSawCleanupFinished.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_ActionOrCleanupThrows_PropagatesFailureAndAllowsNextAction(bool failDuringCleanup)
    {
        // Arrange
        var expected = new InvalidOperationException("test failure");
        var cleanupRan = false;
        var nextActionRan = false;

        // Act
        var exception = await Record.ExceptionAsync(() => WpfTestHelper.RunAsync(() =>
        {
            Dispatcher.CurrentDispatcher.ShutdownFinished += (_, _) =>
            {
                cleanupRan = true;
                if (failDuringCleanup)
                {
                    throw expected;
                }
            };
            if (!failDuringCleanup)
            {
                throw expected;
            }
        }));
        await WpfTestHelper
            .RunAsync(() => nextActionRan = true)
            .WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        exception.Should().BeSameAs(expected);
        cleanupRan.Should().BeTrue();
        nextActionRan.Should().BeTrue();
    }

    [Theory]
    [InlineData(ModifierKeys.None)]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Alt)]
    [InlineData(ModifierKeys.Shift)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Shift)]
    public async Task PressKey_Modifiers_UsesIndependentKeyboardState(ModifierKeys modifiers)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var input = new TextBox();

            // Act
            var args = WpfTestHelper.PressKey(input, Key.Up, modifiers);

            // Assert
            args.KeyboardDevice.Should().NotBeSameAs(Keyboard.PrimaryDevice);
            args.KeyboardDevice.Modifiers.Should().Be(modifiers);
            args.IsDown.Should().BeTrue();
        });
    }

    [Fact]
    public async Task PressKey_UnmodifiedAfterModified_DoesNotRetainPreviousModifiers()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var input = new TextBox();
            WpfTestHelper.PressKey(input, Key.Up, ModifierKeys.Control);

            // Act
            var args = WpfTestHelper.PressKey(input, Key.Down);

            // Assert
            args.KeyboardDevice.Modifiers.Should().Be(ModifierKeys.None);
            args.KeyboardDevice.IsKeyDown(Key.Up).Should().BeFalse();
            args.IsDown.Should().BeTrue();
        });
    }
}
