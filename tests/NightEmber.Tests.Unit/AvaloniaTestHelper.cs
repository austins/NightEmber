using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System.Globalization;

namespace NightEmber.Tests.Unit;

/// <summary>
/// Supports isolated Avalonia unit tests without starting the application or displaying a window.
/// </summary>
/// <remarks>
/// <c>Avalonia.Headless.XUnit</c> does not work with xunit.v3, so tests stay plain <c>[Fact]</c> methods
/// and call <see cref="RunAsync" /> instead. One process-wide <see cref="HeadlessUnitTestSession" /> owns the UI
/// thread and runs actions one at a time, each against a fresh <see cref="App" /> instance, so tests can run
/// in parallel without sharing Avalonia state. The test app uses real Skia drawing so rendered pixels can be read.
/// </remarks>
internal static class AvaloniaTestHelper
{
    private static readonly Lazy<HeadlessUnitTestSession> Session = new(
        static () => HeadlessUnitTestSession.StartNew(typeof(TestApplication)));

    /// <summary>
    /// Runs synchronous test code on the headless UI thread with a new application instance.
    /// </summary>
    /// <param name="action">The test setup, operations, and assertions to execute on the UI thread.</param>
    /// <returns>A task that completes when cleanup finishes, or faults with the test or cleanup exception.</returns>
    /// <remarks>
    /// Dispatcher work queued at background priority or higher runs after the action completes, but the
    /// action itself does not run a message loop. Call <see cref="FlushDispatcher" /> to deliver queued work earlier.
    /// </remarks>
    public static Task RunAsync(Action action)
    {
        // The session reuses one UI thread, so tests that change the culture must not leak it to later tests.
        return Session.Value.Dispatch(
            () =>
            {
                var culture = CultureInfo.CurrentCulture;
                var uiCulture = CultureInfo.CurrentUICulture;
                try
                {
                    action();
                }
                finally
                {
                    CultureInfo.CurrentCulture = culture;
                    CultureInfo.CurrentUICulture = uiCulture;
                }
            },
            CancellationToken.None);
    }

    /// <summary>
    /// Processes dispatcher work queued at background priority or higher on the UI thread.
    /// </summary>
    public static void FlushDispatcher()
    {
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Shows a window and processes the queued activation so keyboard focus and layout are available.
    /// </summary>
    /// <param name="window">The window to show.</param>
    public static void Show(Window window)
    {
        window.Show();
        FlushDispatcher();
        window.UpdateLayout();
    }

    /// <summary>
    /// Shows a control as the only content of a new window.
    /// </summary>
    /// <param name="content">The control to host.</param>
    /// <returns>The shown window; the caller closes it.</returns>
    public static Window ShowInWindow(Control content)
    {
        var window = new Window
        {
            Width = 400,
            Height = 300,
            Content = content
        };
        Show(window);
        return window;
    }

    /// <summary>
    /// Presses and releases a key in the window, routing it to the focused element.
    /// </summary>
    /// <param name="window">The window that receives the key.</param>
    /// <param name="key">The key to press.</param>
    /// <param name="modifiers">The modifier keys held during the press.</param>
    /// <returns>Whether a handler marked the key-down event as handled by the time it reached the window.</returns>
    /// <remarks>The release is skipped when the key press closed the window.</remarks>
    public static bool PressKey(TopLevel window, Key key, KeyModifiers modifiers = KeyModifiers.None)
    {
        var handled = false;
        void Capture(object? sender, KeyEventArgs e)
        {
            handled = e.Handled;
        }

        window.AddHandler(InputElement.KeyDownEvent, Capture, RoutingStrategies.Bubble, handledEventsToo: true);
        try
        {
            var closed = WatchClosed(window);
            window.KeyPress(key, (RawInputModifiers)modifiers, PhysicalKey.None, null);
            if (!closed())
            {
                window.KeyRelease(key, (RawInputModifiers)modifiers, PhysicalKey.None, null);
            }
        }
        finally
        {
            window.RemoveHandler(InputElement.KeyDownEvent, Capture);
        }

        return handled;
    }

    /// <summary>
    /// Presses Alt plus a letter, the way a user triggers an access key.
    /// </summary>
    /// <param name="window">The window that receives the key.</param>
    /// <param name="letter">The access key letter, A to Z.</param>
    /// <remarks>The release is skipped when the access key closed the window.</remarks>
    public static void PressAccessKey(TopLevel window, char letter)
    {
        var physicalKey = Enum.Parse<PhysicalKey>(char.ToUpperInvariant(letter).ToString());
        var closed = WatchClosed(window);
        window.KeyPressQwerty(physicalKey, RawInputModifiers.Alt);
        if (!closed())
        {
            window.KeyReleaseQwerty(physicalKey, RawInputModifiers.Alt);
        }
    }

    private static Func<bool> WatchClosed(TopLevel window)
    {
        var closed = false;
        window.Closed += (_, _) => closed = true;
        return () => closed;
    }

    /// <summary>
    /// Supplies the Avalonia entry point used by <see cref="HeadlessUnitTestSession" />.
    /// </summary>
    internal static class TestApplication
    {
        /// <summary>
        /// Builds the application with the headless platform and real Skia rendering.
        /// </summary>
        /// <returns>The configured builder.</returns>
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder
                .Configure<App>()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
                .UseSkia();
        }
    }
}
