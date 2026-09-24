using Microsoft.Win32.SafeHandles;
using NightEmber.Display;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace NightEmber.Services;

/// <summary>
/// Starts and coordinates the companion process that restores gamma after an
/// unexpected main-process exit.
/// </summary>
internal sealed class WatchdogService : IDisposable
{
    private const string EventPrefix = @"Local\NightEmber.Watchdog.";
    private const string WatchdogSwitch = "--watchdog";
    private const string LauncherSwitch = "--watchdog-launcher";
    private const int InvalidArgumentsExitCode = 2;
    private const int LaunchFailedExitCode = 3;
    private const int LauncherTimeoutMilliseconds = 10_000;
    private readonly EventWaitHandle _orderlyExitEvent;
    private readonly string _eventName;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _orderlyExitEvent.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WatchdogService" /> class with
    /// a unique orderly-exit event.
    /// </summary>
    public WatchdogService()
    {
        _eventName = EventPrefix + Guid.NewGuid().ToString("N");
        _orderlyExitEvent = new EventWaitHandle(false, EventResetMode.ManualReset, _eventName);
    }

    /// <summary>
    /// Starts the current executable in internal watchdog mode.
    /// </summary>
    /// <remarks>
    /// The watchdog receives the main process ID and a randomly named event. Its entry point
    /// dispatches to watchdog mode before loading WPF, so it creates no application,
    /// settings window, tray icon, or controller. It is started through a short-lived launcher
    /// process so it is not a descendant of the main process; tools that kill a whole process
    /// tree, such as an IDE's stop command, therefore leave it running to restore the displays.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown when the watchdog could not be started.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var launcher = StartProcess(LauncherSwitch, Environment.ProcessId, _eventName);
        if (!launcher.WaitForExit(LauncherTimeoutMilliseconds))
        {
            throw new InvalidOperationException("The display cleanup watchdog did not start in time.");
        }

        if (launcher.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"The display cleanup watchdog did not start (exit code {launcher.ExitCode}).");
        }
    }

    /// <summary>
    /// Signals that the main process completed normal gamma cleanup.
    /// </summary>
    public void SignalOrderlyExit()
    {
        if (!_disposed)
        {
            _orderlyExitEvent.Set();
        }
    }

    /// <summary>
    /// Determines whether the command line requests internal watchdog mode.
    /// </summary>
    /// <param name="arguments">The process command-line arguments.</param>
    /// <returns><see langword="true" /> when the first argument is the watchdog or launcher switch.</returns>
    public static bool IsWatchdogCommand(IReadOnlyList<string> arguments)
    {
        return arguments.Count > 0 && IsWatchdogSwitch(arguments[0]);
    }

    /// <summary>
    /// Runs internal watchdog mode from a command line.
    /// </summary>
    /// <param name="arguments">The process command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public static int RunFromCommandLine(IReadOnlyList<string> arguments)
    {
        return RunFromCommandLine(arguments, Run, GammaService.ResetAllDisplays, StartWatchdogProcess);
    }

    /// <summary>
    /// Validates and parses internal watchdog command-line arguments.
    /// </summary>
    /// <param name="arguments">The process command-line arguments.</param>
    /// <param name="processId">The validated main-process ID.</param>
    /// <param name="eventName">The validated orderly-exit event name.</param>
    /// <returns><see langword="true" /> when all watchdog arguments are valid.</returns>
    public static bool TryParseArguments(IReadOnlyList<string> arguments, out int processId, out string eventName)
    {
        processId = 0;
        eventName = string.Empty;

        var parsedProcessId = 0;

        var isValid = arguments.Count == 3
                      && IsWatchdogSwitch(arguments[0])
                      && int.TryParse(
                          arguments[1],
                          NumberStyles.None,
                          CultureInfo.InvariantCulture,
                          out parsedProcessId)
                      && parsedProcessId > 0
                      && arguments[2].StartsWith(EventPrefix, StringComparison.Ordinal)
                      && Guid.TryParseExact(arguments[2][EventPrefix.Length..], "N", out _);

        if (!isValid)
        {
            return false;
        }

        processId = parsedProcessId;
        eventName = arguments[2];
        return true;
    }

    /// <summary>
    /// Identifies process lookup and monitoring failures that trigger the watchdog's recovery path.
    /// </summary>
    /// <param name="exception">The failure to classify.</param>
    /// <returns><see langword="true" /> for argument, invalid-operation, or native Windows failures.</returns>
    public static bool IsProcessMonitoringException(Exception exception)
    {
        return exception is ArgumentException or InvalidOperationException or Win32Exception;
    }

    internal static int RunFromCommandLine(
        IReadOnlyList<string> arguments,
        Action<int, string> run,
        Action resetDisplays,
        Action<int, string> startWatchdog)
    {
        if (!TryParseArguments(arguments, out var processId, out var eventName))
        {
            // Never continue into a normal launch from a malformed internal command line.
            return InvalidArgumentsExitCode;
        }

        if (string.Equals(arguments[0], LauncherSwitch, StringComparison.Ordinal))
        {
            // The launcher exits immediately, leaving the watchdog outside the main process tree.
            try
            {
                startWatchdog(processId, eventName);
                return 0;
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                Trace.TraceWarning("Watchdog launcher could not start the watchdog: {0}", ex.Message);
                return LaunchFailedExitCode;
            }
        }

        try
        {
            run(processId, eventName);
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // A normal exit already reset the displays; after a crash this is the
            // only remaining opportunity to recover a stranded gamma ramp.
            TryResetDisplays(resetDisplays);
        }

        return 0;
    }

    internal static void Run<TState>(
        TState state,
        Func<TState, int> waitForExit,
        Func<TState, bool> isOrderlyExit,
        Action resetDisplays)
    {
        bool shouldReset;
        try
        {
            shouldReset = waitForExit(state) == 1 && !isOrderlyExit(state);
        }
        catch (Exception ex) when (IsProcessMonitoringException(ex))
        {
            // The parent may exit between lookup and handle acquisition. If it did
            // not report an orderly exit, recover the display state immediately.
            shouldReset = !isOrderlyExit(state);
        }

        if (!shouldReset)
        {
            return;
        }

        TryResetDisplays(resetDisplays);
    }

    /// <summary>
    /// Waits for either orderly shutdown or main-process termination.
    /// </summary>
    /// <param name="processId">The main process to monitor.</param>
    /// <param name="eventName">The named event used to signal orderly shutdown.</param>
    /// <remarks>
    /// If the process exits before the event is signaled, this method restores
    /// identity gamma ramps using newly opened display handles.
    /// </remarks>
    private static void Run(int processId, string eventName)
    {
        using var orderlyExitEvent = EventWaitHandle.OpenExisting(eventName);

        // The event is passed as state so the callbacks, which run before it is disposed, do not capture it.
        Run(
            (OrderlyExitEvent: orderlyExitEvent, ProcessId: processId),
            static state => WaitForExit(state.OrderlyExitEvent, state.ProcessId),
            static state => state.OrderlyExitEvent.WaitOne(0),
            GammaService.ResetAllDisplays);
    }

    private static int WaitForExit(WaitHandle orderlyExitEvent, int processId)
    {
        using var parent = Process.GetProcessById(processId);
        using ProcessWaitHandle parentExit = new(parent);
        WaitHandle[] handles = [orderlyExitEvent, parentExit];
        return WaitHandle.WaitAny(handles);
    }

    private static bool IsWatchdogSwitch(string argument)
    {
        return string.Equals(argument, WatchdogSwitch, StringComparison.Ordinal)
               || string.Equals(argument, LauncherSwitch, StringComparison.Ordinal);
    }

    private static void StartWatchdogProcess(int processId, string eventName)
    {
        using var watchdog = StartProcess(WatchdogSwitch, processId, eventName);
    }

    private static Process StartProcess(string mode, int processId, string eventName)
    {
        var executablePath = Environment.ProcessPath
                             ?? throw new InvalidOperationException("The executable path is unavailable.");

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add(mode);
        startInfo.ArgumentList.Add(processId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(eventName);

        return Process.Start(startInfo)
               ?? throw new InvalidOperationException("The display cleanup watchdog did not start.");
    }

    private static void TryResetDisplays(Action resetDisplays)
    {
        try
        {
            resetDisplays();
        }
        catch (Win32Exception ex)
        {
            // No display can be enumerated, so there is no remaining ramp to restore.
            Trace.TraceWarning("Watchdog could not restore neutral gamma: {0}", ex.Message);
        }
    }

    private sealed class ProcessWaitHandle : WaitHandle
    {
        /// <summary>
        /// Initializes a wait handle over a process handle without taking ownership.
        /// </summary>
        /// <param name="process">The process whose exit should signal the handle.</param>
        public ProcessWaitHandle(Process process)
        {
            SafeWaitHandle = new SafeWaitHandle(process.Handle, false);
        }
    }
}
