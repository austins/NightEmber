using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class AppControllerTests
{
    [Fact]
    public void ResolveScheduleState_FirstObservation_PreservesManualOverride()
    {
        // Act
        var result = AppController.ResolveScheduleState(true, null, false);

        // Assert
        result.DesiredState.Should().BeTrue();
        result.ManualOverride.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void ResolveScheduleState_UnchangedSchedule_PreservesManualOverride(bool manualOverride, bool scheduledState)
    {
        // Act
        var result = AppController.ResolveScheduleState(manualOverride, scheduledState, scheduledState);

        // Assert
        result.DesiredState.Should().Be(manualOverride);
        result.ManualOverride.Should().Be(manualOverride);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    public void ResolveScheduleState_ScheduleBoundary_ExpiresManualOverride(
        bool manualOverride,
        bool previousScheduledState,
        bool scheduledState)
    {
        // Act
        var result = AppController.ResolveScheduleState(manualOverride, previousScheduledState, scheduledState);

        // Assert
        result.DesiredState.Should().Be(scheduledState);
        result.ManualOverride.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolveScheduleState_NoManualOverride_FollowsSchedule(bool scheduledState)
    {
        // Act
        var result = AppController.ResolveScheduleState(null, !scheduledState, scheduledState);

        // Assert
        result.DesiredState.Should().Be(scheduledState);
        result.ManualOverride.Should().BeNull();
    }

    [Theory]
    [InlineData(-100, 400, 0)]
    [InlineData(0, 400, 0)]
    [InlineData(100, 400, 0.25)]
    [InlineData(400, 400, 1)]
    [InlineData(500, 400, 1)]
    [InlineData(0, 0, 1)]
    public void CalculateFadeProgress_ElapsedTime_ReturnsClampedProgress(
        int elapsedMilliseconds,
        int durationMilliseconds,
        double expected)
    {
        // Act
        var result = AppController.CalculateFadeProgress(
            TimeSpan.FromMilliseconds(elapsedMilliseconds),
            TimeSpan.FromMilliseconds(durationMilliseconds));

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(0.25, 5800, 95)]
    [InlineData(0.5, 5000, 90)]
    [InlineData(1, 3400, 80)]
    [InlineData(1.25, 3400, 80)]
    public void InterpolateGammaState_FadeProgress_InterpolatesAndClampsAtTarget(
        double progress,
        double expectedKelvin,
        double expectedBrightness)
    {
        // Act
        var result = AppController.InterpolateGammaState(6600, 100, 3400, 80, progress);

        // Assert
        result.Kelvin.Should().Be(expectedKelvin);
        result.Brightness.Should().Be(expectedBrightness);
    }

    [Theory]
    [InlineData(3400, 80, 3400, 80, true)]
    [InlineData(3400, 80, 3400.009, 80.009, true)]
    [InlineData(3400, 80, 3400.01, 80, false)]
    [InlineData(3400, 80, 3400, 80.01, false)]
    public void GammaStatesMatch_States_UsesExclusiveTolerance(
        double firstKelvin,
        double firstBrightness,
        double secondKelvin,
        double secondBrightness,
        bool expected)
    {
        // Act
        var result = AppController.GammaStatesMatch(firstKelvin, firstBrightness, secondKelvin, secondBrightness);

        // Assert
        result.Should().Be(expected);
    }
}
