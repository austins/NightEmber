namespace NightEmber.Services;

internal sealed class MessageThrottle
{
    private readonly TimeSpan _interval;
    private readonly List<AllowedMessage> _recentMessages = [];

    /// <summary>
    /// Initializes a throttle that suppresses repeated messages within a time interval.
    /// </summary>
    /// <param name="interval">The positive interval before an identical message can be shown again.</param>
    /// <exception cref="ArgumentOutOfRangeException">The interval is not positive.</exception>
    public MessageThrottle(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be positive.");
        }

        _interval = interval;
    }

    /// <summary>
    /// Records a message when it has not been shown within the suppression interval.
    /// </summary>
    /// <param name="message">The message to compare using ordinal equality.</param>
    /// <param name="timestamp">The current UTC time used to expire previous messages.</param>
    /// <returns>Whether the message should be shown.</returns>
    public bool ShouldAllow(string message, DateTime timestamp)
    {
        _recentMessages.RemoveAll(entry => timestamp - entry.Timestamp >= _interval);
        if (_recentMessages.Any(entry => string.Equals(entry.Message, message, StringComparison.Ordinal)))
        {
            return false;
        }

        _recentMessages.Add(new AllowedMessage(message, timestamp));
        return true;
    }

    private sealed record AllowedMessage(string Message, DateTime Timestamp);
}
