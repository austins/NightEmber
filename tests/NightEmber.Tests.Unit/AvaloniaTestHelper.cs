using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;

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
        return Session.Value.Dispatch(action, CancellationToken.None);
    }

    /// <summary>
    /// Processes dispatcher work queued at background priority or higher on the UI thread.
    /// </summary>
    public static void FlushDispatcher()
    {
        Dispatcher.UIThread.RunJobs();
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
