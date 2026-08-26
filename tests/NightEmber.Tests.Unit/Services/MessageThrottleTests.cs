using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class MessageThrottleTests
{
    private static readonly DateTime InitialTime = new(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_NonPositiveInterval_Throws()
    {
        // Act
        var action = () => new MessageThrottle(TimeSpan.Zero);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ShouldAllow_FirstMessage_ReturnsTrue()
    {
        // Arrange
        var throttle = CreateThrottle();

        // Act
        var result = throttle.ShouldAllow("Display error", InitialTime);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllow_IdenticalMessageWithinInterval_ReturnsFalse()
    {
        // Arrange
        var throttle = CreateThrottle();
        throttle.ShouldAllow("Display error", InitialTime);

        // Act
        var result = throttle.ShouldAllow("Display error", InitialTime.AddSeconds(59));

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAllow_DifferentMessageWithinInterval_ReturnsTrue()
    {
        // Arrange
        var throttle = CreateThrottle();
        throttle.ShouldAllow("Display error", InitialTime);

        // Act
        var result = throttle.ShouldAllow("Driver error", InitialTime.AddSeconds(1));

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllow_IdenticalMessageAtIntervalBoundary_ReturnsTrue()
    {
        // Arrange
        var throttle = CreateThrottle();
        throttle.ShouldAllow("Display error", InitialTime);

        // Act
        var result = throttle.ShouldAllow("Display error", InitialTime.AddMinutes(1));

        // Assert
        result.Should().BeTrue();
    }

    private static MessageThrottle CreateThrottle()
    {
        return new MessageThrottle(TimeSpan.FromMinutes(1));
    }
}
