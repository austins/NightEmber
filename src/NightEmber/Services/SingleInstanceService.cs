namespace NightEmber.Services;

/// <summary>
/// Enforces one Night Ember instance per interactive Windows session.
/// </summary>
internal sealed class SingleInstanceService : IDisposable
{
    private const string MutexName = @"Local\NightEmber.Application";
    private const string ActivationEventName = @"Local\NightEmber.Activate";
    private const int ActivationRetryCount = 10;
    private const int ActivationRetryDelayMilliseconds = 100;

    private readonly Mutex _mutex = new(false, MutexName);
    private readonly EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _registeredWait;
    private bool _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _registeredWait?.Unregister(null);
        _activationEvent?.Dispose();
        if (IsPrimaryInstance)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process is already exiting and no longer owns the mutex.
            }
        }

        _mutex.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SingleInstanceService" /> class
    /// and attempts to acquire primary-instance ownership.
    /// </summary>
    public SingleInstanceService()
    {
        try
        {
            IsPrimaryInstance = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            IsPrimaryInstance = true;
        }

        if (IsPrimaryInstance)
        {
            _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this process owns the application mutex.
    /// </summary>
    public bool IsPrimaryInstance { get; }

    /// <summary>
    /// Signals the primary instance to show its settings window.
    /// </summary>
    /// <remarks>
    /// A short retry window handles a primary process that owns the mutex but has
    /// not yet finished creating its activation event.
    /// </remarks>
    public static void SignalPrimaryInstance()
    {
        for (var attempt = 0; attempt < ActivationRetryCount; attempt++)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(ActivationEventName);
                activationEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(ActivationRetryDelayMilliseconds);
            }
        }
    }

    /// <summary>
    /// Registers a callback for activation requests from subsequent launches.
    /// </summary>
    /// <param name="callback">The callback to invoke on a thread-pool thread.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called by a process that is not the primary instance.
    /// </exception>
    public void Listen(Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_activationEvent is null)
        {
            throw new InvalidOperationException("Only the primary instance can listen.");
        }

        _registeredWait?.Unregister(null);
        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut)
                {
                    callback();
                }
            },
            null,
            Timeout.Infinite,
            false);
    }
}
