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
    public void ResolveScheduleState_UnchangedSchedule_PreservesManualOverride(
        bool manualOverride,
        bool scheduledState)
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
        var result = AppController.ResolveScheduleState(
            manualOverride,
            previousScheduledState,
            scheduledState);

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
    [InlineData(0, 2)]
    [InlineData(20, 2)]
    [InlineData(40, 2)]
    [InlineData(60, 3)]
    [InlineData(300, 15)]
    [InlineData(5000, 250)]
    public void CalculateFadeStepCount_FadeDuration_ReturnsTwentyMillisecondStepCount(
        int fadeMilliseconds,
        int expected)
    {
        // Act
        var result = AppController.CalculateFadeStepCount(fadeMilliseconds);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(1, 4, 5800, 95)]
    [InlineData(2, 4, 5000, 90)]
    [InlineData(4, 4, 3400, 80)]
    [InlineData(5, 4, 3400, 80)]
    public void InterpolateGammaState_FadeStep_InterpolatesAndClampsAtTarget(
        int currentStep,
        int stepCount,
        double expectedKelvin,
        double expectedBrightness)
    {
        // Act
        var result = AppController.InterpolateGammaState(6600, 100, 3400, 80, currentStep, stepCount);

        // Assert
        result.Kelvin.Should().Be(expectedKelvin);
        result.Brightness.Should().Be(expectedBrightness);
    }
}
