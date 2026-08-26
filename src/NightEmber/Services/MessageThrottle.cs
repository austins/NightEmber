namespace NightEmber.Services;

internal sealed class MessageThrottle
{
    private readonly TimeSpan _interval;
    private readonly List<AllowedMessage> _recentMessages = [];

    public MessageThrottle(TimeSpan interval)
    {
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), interval, "The interval must be positive.");
        }

        _interval = interval;
    }

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
