using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NightEmber.Tests.Unit;

/// <summary>
/// Supports isolated WPF unit tests without starting the application or displaying a window.
/// </summary>
/// <remarks>
/// WPF controls require a single-threaded apartment. This helper supplies an STA thread
/// and synthetic routed keyboard events without requiring a visible window or injecting OS keyboard input.
/// Actions are serialized because WPF's shared XAML metadata can be initialized by multiple control types.
/// </remarks>
internal static class WpfTestHelper
{
    private static readonly Lock ExecutionLock = new();

    /// <summary>
    /// Runs synchronous test code on a dedicated background STA thread and shuts down its dispatcher afterward.
    /// </summary>
    /// <param name="action">The test setup, operations, and assertions to execute on the STA thread.</param>
    /// <returns>A task that completes when cleanup finishes, or faults with the test or cleanup exception.</returns>
    /// <remarks>
    /// This method does not run a dispatcher message loop. Tests must not depend on queued dispatcher work
    /// or real-time timer ticks being processed during the action.
    /// </remarks>
    public static Task RunAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                lock (ExecutionLock)
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        Dispatcher.CurrentDispatcher.InvokeShutdown();
                    }
                }

                completion.SetResult();
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    /// <summary>
    /// Raises a synthetic preview key-down event directly on a WPF element.
    /// </summary>
    /// <param name="element">The element whose routed input handlers should receive the event.</param>
    /// <param name="key">The key represented by the event.</param>
    /// <returns>The event arguments, allowing assertions against properties such as <see cref="RoutedEventArgs.Handled" />.</returns>
    /// <remarks>
    /// Call this on the element's owning STA thread. It exercises routed handlers, not native keyboard delivery,
    /// text insertion, or physical key state.
    /// </remarks>
    public static KeyEventArgs PressKey(UIElement element, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, new TestPresentationSource(), 0, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent
        };
        element.RaiseEvent(args);
        return args;
    }

    /// <summary>
    /// Supplies the presentation source required by keyboard event arguments without allocating a native window.
    /// </summary>
    private sealed class TestPresentationSource : PresentationSource
    {
        public override Visual RootVisual { get; set; } = null!;

        public override bool IsDisposed => false;

        /// <summary>
        /// Rejects rendering requests because this source exists only to construct synthetic input events.
        /// </summary>
        /// <returns>This method always throws.</returns>
        /// <exception cref="NotSupportedException">Always thrown because the test source has no rendering target.</exception>
        protected override CompositionTarget GetCompositionTargetCore()
        {
            throw new NotSupportedException("Unit-test input events do not require a rendered window.");
        }
    }
}
