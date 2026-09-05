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

    [Fact]
    public void ShouldAllow_InterleavedMessages_TracksEachCooldownIndependently()
    {
        // Arrange
        var throttle = CreateThrottle();
        throttle.ShouldAllow("First", InitialTime);
        throttle.ShouldAllow("Second", InitialTime.AddSeconds(30));

        // Act
        var firstExpired = throttle.ShouldAllow("First", InitialTime.AddSeconds(60));
        var secondWithinInterval = throttle.ShouldAllow("Second", InitialTime.AddSeconds(60));
        var secondExpired = throttle.ShouldAllow("Second", InitialTime.AddSeconds(90));

        // Assert
        firstExpired.Should().BeTrue();
        secondWithinInterval.Should().BeFalse();
        secondExpired.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllow_RejectedMessage_DoesNotExtendCooldown()
    {
        // Arrange
        var throttle = CreateThrottle();
        throttle.ShouldAllow("Display error", InitialTime);

        // Act
        var beforeBoundary = throttle.ShouldAllow("Display error", InitialTime.AddSeconds(59));
        var atBoundary = throttle.ShouldAllow("Display error", InitialTime.AddMinutes(1));
        var afterBoundary = throttle.ShouldAllow("Display error", InitialTime.AddSeconds(61));

        // Assert
        beforeBoundary.Should().BeFalse();
        atBoundary.Should().BeTrue();
        afterBoundary.Should().BeFalse();
    }

    [Fact]
    public void ShouldAllow_MessageComparison_IsCaseSensitive()
    {
        // Arrange
        var throttle = CreateThrottle();

        // Act
        var first = throttle.ShouldAllow("Display error", InitialTime);
        var differentCase = throttle.ShouldAllow("display error", InitialTime);
        var repeated = throttle.ShouldAllow("Display error", InitialTime);

        // Assert
        first.Should().BeTrue();
        differentCase.Should().BeTrue();
        repeated.Should().BeFalse();
    }

    private static MessageThrottle CreateThrottle()
    {
        return new MessageThrottle(TimeSpan.FromMinutes(1));
    }
}
