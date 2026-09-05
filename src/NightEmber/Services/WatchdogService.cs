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
    private readonly EventWaitHandle _orderlyExitEvent;
    private readonly string _eventName;
    private Process? _watchdogProcess;
    private bool _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _watchdogProcess?.Dispose();
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
    /// The child receives the main process ID and a randomly named event. It does
    /// not initialize WPF UI or application services.
    /// </remarks>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var executablePath = Environment.ProcessPath
                             ?? throw new InvalidOperationException("The executable path is unavailable.");

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };
        startInfo.ArgumentList.Add("--watchdog");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add(_eventName);

        _watchdogProcess?.Dispose();
        _watchdogProcess = Process.Start(startInfo)
                           ?? throw new InvalidOperationException("The display cleanup watchdog did not start.");
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
                      && string.Equals(arguments[0], "--watchdog", StringComparison.Ordinal)
                      && int.TryParse(
                          arguments[1],
                          NumberStyles.None,
                          CultureInfo.InvariantCulture,
                          out parsedProcessId)
                      && parsedProcessId > 0
                      && arguments[2].StartsWith(EventPrefix, StringComparison.Ordinal)
                      && Guid.TryParseExact(arguments[2][EventPrefix.Length..], "N", out _);

        if (isValid)
        {
            processId = parsedProcessId;
            eventName = arguments[2];
        }

        return isValid;
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
    public static void Run(int processId, string eventName)
    {
        using var orderlyExitEvent = EventWaitHandle.OpenExisting(eventName);

        Run(
            () =>
            {
                using var parent = Process.GetProcessById(processId);
                using ProcessWaitHandle parentExit = new(parent);
                WaitHandle[] handles = [orderlyExitEvent, parentExit];
                return WaitHandle.WaitAny(handles);
            },
            () => orderlyExitEvent.WaitOne(0),
            GammaService.ResetAllDisplays);
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

    internal static void Run(Func<int> waitForExit, Func<bool> isOrderlyExit, Action resetDisplays)
    {
        try
        {
            var signaledHandle = waitForExit();
            if (signaledHandle == 1 && !isOrderlyExit())
            {
                resetDisplays();
            }
        }
        catch (Exception ex) when (IsProcessMonitoringException(ex))
        {
            // The parent may exit between lookup and handle acquisition. If it did
            // not report an orderly exit, recover the display state immediately.
            if (!isOrderlyExit())
            {
                resetDisplays();
            }
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
