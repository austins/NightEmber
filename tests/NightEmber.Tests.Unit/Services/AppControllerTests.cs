using Microsoft.Win32;
using NightEmber.Display;
using NightEmber.Models;
using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class AppControllerTests
{
    [Fact]
    public void Preview_DuplicateSuccessfulValues_AppliesOnceAndCancelRestoresSavedState()
    {
        // Arrange
        using var fixture = new ControllerFixture();

        // Act
        fixture.Controller.Preview(4000, 80);
        fixture.Controller.Preview(4000, 80);
        fixture.Controller.UpdateSchedule(true);
        var previewStates = fixture.Gamma.Applied.ToArray();
        fixture.Controller.CancelPreview();
        fixture.Controller.CancelPreview();

        // Assert
        previewStates.Should().Equal((4000d, 80d));
        fixture.Gamma.Applied.Should().Equal((4000d, 80d), (3400d, 75d));
        fixture.Controller.CurrentSettings.Temperature.Should().Be(3400);
    }

    [Fact]
    public void Preview_RejectedRamp_IsRetriedRatherThanRemembered()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Gamma.ApplyResults.Enqueue(false);

        // Act
        fixture.Controller.Preview(4000, 80);
        fixture.Controller.Preview(4000, 80);

        // Assert
        fixture.Gamma.Applied.Should().Equal((4000d, 80d), (4000d, 80d));
    }

    [Fact]
    public void Preview_DuringFade_StopsOldTimerAndCancelReturnsToSavedTarget()
    {
        // Arrange
        using var fixture = new ControllerFixture(1000);
        fixture.Controller.UpdateSchedule(false);
        var fade = fixture.Runtime.Timers[^1];
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(500);
        fade.Fire();

        // Act
        fixture.Controller.Preview(4200, 90);
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(1000);
        fade.Fire();
        fixture.Controller.CancelPreview();

        // Assert
        fade.IsRunning.Should().BeFalse();
        fixture.Gamma.Applied.Should().Equal((4950d, 87.5d), (4200d, 90d), (3400d, 75d));
    }

    [Fact]
    public void Toggle_DuringPreview_EndsPreviewAndFollowsLogicalTintState()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Controller.UpdateSchedule(true);
        fixture.Controller.Preview(4200, 90);

        // Act
        fixture.Controller.Toggle();
        fixture.Controller.CancelPreview();
        fixture.Controller.UpdateSchedule(false);

        // Assert
        fixture.Gamma.Applied.Should().Equal((3400d, 75d), (4200d, 90d), (6500d, 100d));
    }

    [Fact]
    public void Toggle_DuringFade_StartsReverseFadeFromLastAppliedValues()
    {
        // Arrange
        using var fixture = new ControllerFixture(1000);
        fixture.Controller.UpdateSchedule(false);
        var firstFade = fixture.Runtime.Timers[^1];
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(500);
        firstFade.Fire();

        // Act
        fixture.Controller.Toggle();
        var reverseFade = fixture.Runtime.Timers[^1];
        firstFade.Fire();
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(1000);
        reverseFade.Fire();

        var intermediateStates = fixture.Gamma.Applied.ToArray();
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(1500);
        reverseFade.Fire();

        // Assert
        firstFade.IsRunning.Should().BeFalse();
        intermediateStates.Should().Equal((4950d, 87.5d), (5725d, 93.75d));
        reverseFade.IsRunning.Should().BeFalse();
        fixture.Gamma.Applied[^1].Should().Be((6500d, 100d));
    }

    [Fact]
    public void UpdateSchedule_ManualOverride_ExpiresAtNextBoundary()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Runtime.Now = new DateTime(2026, 9, 4, 20, 0, 0, DateTimeKind.Local);
        fixture.Controller.UpdateSchedule(false);

        // Act
        fixture.Controller.Toggle();
        fixture.Controller.UpdateSchedule(false);
        fixture.Runtime.Now = fixture.Runtime.Now.AddHours(1);
        fixture.Controller.UpdateSchedule(false);
        fixture.Runtime.Now = fixture.Runtime.Now.AddHours(10);
        fixture.Controller.UpdateSchedule(false);

        // Assert
        fixture.Gamma.Applied.Should().Equal((3400d, 75d), (6500d, 100d));
    }

    [Fact]
    public void SaveSettings_PreviewActive_PersistsNormalizedSettingsBeforeApplyAndStartup()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Controller.Preview(4200, 90);
        fixture.Events.Clear();
        var settings = new AppSettings
        {
            Temperature = 100,
            Brightness = 80,
            Mode = ScheduleMode.Custom,
            FadeMs = 0
        };

        // Act
        var warning = fixture.Controller.SaveSettings(settings, true);
        fixture.Controller.CancelPreview();

        // Assert
        warning.Should().BeNull();
        fixture.Events.Should().Equal("save", "apply", "startup:True");
        fixture.Runtime.Saved.Should().NotBeNull();
        fixture.Runtime.Saved.Temperature.Should().Be(3400);
        fixture.Controller.CurrentSettings.Should().BeEquivalentTo(fixture.Runtime.Saved);
        fixture.Gamma.Applied[^1].Should().Be((3400d, 80d));
    }

    [Fact]
    public void SaveSettings_SaveFails_PreservesPreviewAndPreviouslySavedConfiguration()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Controller.Preview(4200, 90);
        fixture.Runtime.SaveException = new IOException("disk full");
        fixture.Events.Clear();

        // Act
        var exception = Record.Exception(() =>
            fixture.Controller.SaveSettings(new AppSettings { Temperature = 5000 }, true));

        fixture.Controller.UpdateSchedule(true);
        var eventsAfterFailedSave = fixture.Events.ToArray();
        fixture.Controller.CancelPreview();

        // Assert
        exception.Should().BeOfType<IOException>().Which.Message.Should().Be("disk full");
        eventsAfterFailedSave.Should().Equal("save");
        fixture.Controller.CurrentSettings.Temperature.Should().Be(3400);
        fixture.Gamma.Applied[^1].Should().Be((3400d, 75d));
    }

    [Fact]
    public void SaveSettings_StartupFails_ReturnsWarningAfterSavedStateIsApplied()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Runtime.StartupException = new UnauthorizedAccessException("denied");

        // Act
        var warning = fixture.Controller.SaveSettings(
            new AppSettings
            {
                Temperature = 5000,
                Mode = ScheduleMode.Custom,
                FadeMs = 0
            },
            true);

        // Assert
        warning.Should().Contain("Settings were saved").And.Contain("denied");
        fixture.Events.Should().Equal("save", "apply", "startup:True");
        fixture.Controller.CurrentSettings.Temperature.Should().Be(5000);
        fixture.Gamma.Applied.Should().Equal((5000d, 100d));
    }

    [Fact]
    public void SaveSettings_ManualOverrideActive_ClearsOverrideAndReturnsIndependentSettings()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Controller.UpdateSchedule(true);
        fixture.Controller.Toggle();
        var settings = fixture.Controller.CurrentSettings;

        // Act
        fixture.Controller.SaveSettings(settings, false);

        // Assert
        fixture.Gamma.Applied[^1].Should().Be((3400d, 75d));
        fixture.Controller.CurrentSettings.Should().NotBeSameAs(settings);
        fixture.Controller.CurrentSettings.Should().NotBeSameAs(fixture.Controller.CurrentSettings);
    }

    [Fact]
    public void SaveSettings_UnrelatedStartupFailure_PropagatesAfterPersistingAndApplying()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Runtime.StartupException = new NotSupportedException("unrelated");

        // Act
        Action save = () => fixture.Controller.SaveSettings(fixture.Controller.CurrentSettings, false);

        // Assert
        save.Should().Throw<NotSupportedException>().WithMessage("unrelated");
        fixture.Events.Should().Equal("save", "apply", "startup:False");
    }

    [Theory]
    [InlineData(PowerModes.Resume, 1)]
    [InlineData(PowerModes.Suspend, 0)]
    [InlineData(PowerModes.StatusChange, 0)]
    public void OnPowerModeChanged_OnlyResume_PostsReapply(PowerModes mode, int expectedPosts)
    {
        // Arrange
        using var fixture = new ControllerFixture();

        // Act
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(mode));

        // Assert
        fixture.Runtime.Posted.Should().HaveCount(expectedPosts);
        fixture.Gamma.Applied.Should().BeEmpty();
    }

    [Theory]
    [InlineData(SessionSwitchReason.SessionUnlock, 1)]
    [InlineData(SessionSwitchReason.SessionLock, 0)]
    [InlineData(SessionSwitchReason.SessionLogon, 0)]
    public void OnSessionSwitch_OnlyUnlock_PostsReapply(SessionSwitchReason reason, int expectedPosts)
    {
        // Arrange
        using var fixture = new ControllerFixture();

        // Act
        fixture.Controller.OnSessionSwitch(fixture, new SessionSwitchEventArgs(reason));

        // Assert
        fixture.Runtime.Posted.Should().HaveCount(expectedPosts);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReapplyDisplays_RepeatedNotifications_DebouncesAndRestoresPreviewOrScheduledState(bool preview)
    {
        // Arrange
        using var fixture = new ControllerFixture();
        if (preview)
        {
            fixture.Controller.Preview(4200, 90);
        }

        fixture.Events.Clear();

        // Act
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(PowerModes.Resume));
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(PowerModes.Resume));
        fixture.Runtime.DrainPosted();
        var reapply = fixture.Runtime.Timers[2];

        var eventsBeforeTick = fixture.Events.ToArray();
        reapply.Fire();

        // Assert
        reapply.StartCount.Should().Be(2);
        reapply.Interval.Should().Be(TimeSpan.FromMilliseconds(1200));
        eventsBeforeTick.Should().BeEmpty();
        fixture.Events.Should().Equal("open", "apply");
        reapply.IsRunning.Should().BeFalse();
        fixture.Gamma.Applied[^1].Should().Be(preview ? (4200d, 90d) : (3400d, 75d));
    }

    [Fact]
    public void ReapplyDisplays_QueuedBeforeExit_DoesNotRestartTimerAfterShutdown()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(PowerModes.Resume));

        // Act
        fixture.Controller.Exit();
        fixture.Runtime.DrainPosted();
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(PowerModes.Resume));

        // Assert
        fixture.Runtime.Timers.Should().AllSatisfy(static timer => timer.IsRunning.Should().BeFalse());
        fixture.Runtime.Timers[2].StartCount.Should().Be(0);
        fixture.Runtime.Posted.Should().BeEmpty();
    }

    [Fact]
    public void ReapplyDisplays_DisplayOpenFails_DoesNotApplyAndAllowsLaterRetry()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Gamma.OpenException = new System.ComponentModel.Win32Exception("display unavailable");

        // Act
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(PowerModes.Resume));
        fixture.Runtime.DrainPosted();
        fixture.Runtime.Timers[2].Fire();

        var eventsAfterFailure = fixture.Events.ToArray();
        var timerRunningAfterFailure = fixture.Runtime.Timers[2].IsRunning;
        fixture.Gamma.OpenException = null;
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(PowerModes.Resume));
        fixture.Runtime.DrainPosted();
        fixture.Runtime.Timers[2].Fire();

        // Assert
        eventsAfterFailure.Should().Equal("open");
        timerRunningAfterFailure.Should().BeFalse();
        fixture.Events.Should().Equal("open", "open", "apply");
    }

    [Fact]
    public void RepairGammaDrift_TintStates_OnlyRepairsActiveStableTint()
    {
        // Arrange
        using var fixture = new ControllerFixture(1000);
        var drift = fixture.Runtime.Timers[1];
        drift.Start();

        // Act
        drift.Fire();
        fixture.Controller.UpdateSchedule(false);
        drift.Fire();
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(1000);
        fixture.Runtime.Timers[^1].Fire();
        drift.Fire();
        fixture.Controller.Preview(4200, 90);
        drift.Fire();
        fixture.Controller.CancelPreview();
        drift.Fire();
        fixture.Controller.Exit();
        drift.Fire();

        // Assert
        fixture.Events.Count(static entry => entry == "repair").Should().Be(2);
    }

    [Fact]
    public void RepairGammaDrift_RecognizedFailure_IsRecoverableButUnrelatedFailurePropagates()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Controller.UpdateSchedule(true);
        var drift = fixture.Runtime.Timers[1];
        drift.Start();
        fixture.Gamma.RepairException = new System.ComponentModel.Win32Exception();

        // Act
        drift.Fire();
        fixture.Gamma.RepairException = null;
        drift.Fire();
        fixture.Gamma.RepairException = new IOException("unrelated");
        var repair = drift.Fire;

        // Assert
        repair.Should().Throw<IOException>().WithMessage("unrelated");
        fixture.Events.Count(static entry => entry == "repair").Should().Be(3);
    }

    [Fact]
    public void ApplyGamma_RecognizedDisplayException_StopsFadeWithoutAdvancingRememberedState()
    {
        // Arrange
        using var fixture = new ControllerFixture(1000);
        fixture.Controller.UpdateSchedule(false);
        var fade = fixture.Runtime.Timers[^1];
        fixture.Gamma.ApplyException = new System.ComponentModel.Win32Exception("display unavailable");
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(500);

        // Act
        fade.Fire();

        var timerRunningAfterFailure = fade.IsRunning;
        fixture.Gamma.ApplyException = null;
        fixture.Controller.Preview(6500, 100);

        // Assert
        timerRunningAfterFailure.Should().BeFalse();
        fixture.Gamma.Applied.Should().HaveCount(1);
    }

    [Fact]
    public void Preview_UnrelatedException_Propagates()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Gamma.ApplyException = new IOException("unrelated");

        // Act
        var preview = () => fixture.Controller.Preview(4200, 90);

        // Assert
        preview.Should().Throw<IOException>().WithMessage("unrelated");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Exit_ResetFails_UsesFallbackBeforeShutdownAndDisposesOnce(bool throws)
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Gamma.ResetResult = false;
        if (throws)
        {
            fixture.Gamma.ResetException = new System.ComponentModel.Win32Exception();
        }

        // Act
        fixture.Controller.Exit();
        fixture.Controller.Exit();
        fixture.Controller.Dispose();
        fixture.Controller.Dispose();
        fixture.Controller.Toggle();
        fixture.Controller.Preview(4200, 90);
        fixture.Controller.CancelPreview();

        // Assert
        fixture.Events.Should().Equal("reset", "reset-all", "shutdown", "dispose");
        fixture.Runtime.Timers.Should().AllSatisfy(static timer => timer.IsRunning.Should().BeFalse());
    }

    [Fact]
    public void Dispose_WithoutExit_ResetsBeforeReleasingGamma()
    {
        // Arrange
        using var fixture = new ControllerFixture();

        // Act
        fixture.Controller.Dispose();
        fixture.Controller.Dispose();

        // Assert
        fixture.Events.Should().Equal("reset", "dispose");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OnSessionEnding_OnOrOffDispatcher_ResetsSynchronouslyWithoutShutdown(bool hasAccess)
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Runtime.HasAccess = hasAccess;
        string[] expected = hasAccess ? ["reset", "dispose"] : ["invoke", "reset", "dispose"];

        // Act
        fixture.Controller.OnSessionEnding(fixture, new SessionEndingEventArgs(SessionEndReasons.Logoff));
        fixture.Controller.Dispose();

        // Assert
        fixture.Events.Should().Equal(expected);
    }

    [Fact]
    public void OnSessionEnding_UnavailableDispatcher_FallsBackToIndependentReset()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Runtime.HasAccess = false;
        fixture.Runtime.InvokeException = new InvalidOperationException("dispatcher stopped");

        // Act
        fixture.Controller.OnSessionEnding(fixture, new SessionEndingEventArgs(SessionEndReasons.SystemShutdown));

        // Assert
        fixture.Events.Should().Equal("invoke", "reset-all");
    }

    [Fact]
    public void Exit_UnrelatedResetFailure_PropagatesWithoutShutdownOrFallback()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Gamma.ResetException = new IOException("unrelated");

        // Act
        var exit = fixture.Controller.Exit;

        // Assert
        exit.Should().Throw<IOException>().WithMessage("unrelated");
        fixture.Events.Should().Equal("reset");
    }

    [Fact]
    public void OnSessionEnding_StopsTimersAndIgnoresLaterBackgroundWork()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Controller.UpdateSchedule(true);
        var drift = fixture.Runtime.Timers[1];
        fixture.Controller.OnSessionEnding(fixture, new SessionEndingEventArgs(SessionEndReasons.Logoff));
        var timersRunning = fixture.Runtime.Timers.Select(static timer => timer.IsRunning).ToArray();
        fixture.Events.Clear();

        // Act
        fixture.Controller.UpdateSchedule(true);
        fixture.Controller.Toggle();
        fixture.Controller.OnPowerModeChanged(fixture, new PowerModeChangedEventArgs(PowerModes.Resume));
        fixture.Controller.OnTimeChanged(fixture, EventArgs.Empty);
        fixture.Runtime.DrainPosted();
        drift.Start();
        drift.Fire();

        // Assert
        timersRunning.Should().AllSatisfy(static running => running.Should().BeFalse());
        fixture.Events.Should().BeEmpty();
        fixture.Gamma.Applied.Should().Equal((3400d, 75d));
    }

    [Fact]
    public void Exit_FallbackResetThrowsDisplayException_StillShutsDown()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Gamma.ResetResult = false;
        fixture.Gamma.ResetAllException = new System.ComponentModel.Win32Exception("no displays");

        // Act
        fixture.Controller.Exit();

        // Assert
        fixture.Events.Should().Equal("reset", "reset-all", "shutdown");
    }

    [Fact]
    public void OnSessionEnding_UnavailableDispatcherAndResetAllThrows_DoesNotPropagate()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Runtime.HasAccess = false;
        fixture.Runtime.InvokeException = new InvalidOperationException("dispatcher stopped");
        fixture.Gamma.ResetAllException = new System.ComponentModel.Win32Exception("no displays");

        // Act
        var sessionEnding = () => fixture.Controller.OnSessionEnding(
            fixture,
            new SessionEndingEventArgs(SessionEndReasons.Logoff));

        // Assert
        sessionEnding.Should().NotThrow();
        fixture.Events.Should().Equal("invoke", "reset-all");
    }

    [Fact]
    public void OnTimeChanged_RefreshesTimeZoneBeforeReevaluatingSchedule()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Runtime.Now = new DateTime(2026, 9, 4, 12, 0, 0, DateTimeKind.Local);
        fixture.Controller.UpdateSchedule(true);
        fixture.Events.Clear();
        fixture.Runtime.Now = new DateTime(2026, 9, 4, 22, 0, 0, DateTimeKind.Local);

        // Act
        fixture.Controller.OnTimeChanged(fixture, EventArgs.Empty);
        var eventsBeforeDispatch = fixture.Events.ToArray();
        fixture.Runtime.DrainPosted();

        // Assert
        eventsBeforeDispatch.Should().BeEmpty();
        fixture.Events.Should().Equal("refresh-tz", "apply");
        fixture.Gamma.Applied[^1].Should().Be((3400d, 75d));
    }

    [Fact]
    public void RepairGammaDrift_PreviousWriteRejected_RetriesTargetInsteadOfRepairing()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        fixture.Gamma.ApplyResults.Enqueue(false);
        fixture.Controller.UpdateSchedule(true);
        var drift = fixture.Runtime.Timers[1];
        fixture.Events.Clear();

        // Act
        drift.Fire();
        drift.Fire();

        // Assert
        fixture.Events.Should().Equal("apply", "repair");
        fixture.Gamma.Applied.Should().Equal((3400d, 75d), (3400d, 75d));
    }

    [Fact]
    public void DriftMonitoring_RunsOnlyWhileTintIsOn()
    {
        // Arrange
        using var fixture = new ControllerFixture();
        var drift = fixture.Runtime.Timers[1];
        var runningInitially = drift.IsRunning;

        // Act
        fixture.Controller.UpdateSchedule(true);
        var runningWhileOn = drift.IsRunning;
        fixture.Controller.Toggle();

        // Assert
        runningInitially.Should().BeFalse();
        runningWhileOn.Should().BeTrue();
        drift.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void ApplyGamma_RejectedDuringFade_StopsFade()
    {
        // Arrange
        using var fixture = new ControllerFixture(1000);
        fixture.Controller.UpdateSchedule(false);
        var fade = fixture.Runtime.Timers[^1];
        fixture.Gamma.ApplyResults.Enqueue(false);
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(500);

        // Act
        fade.Fire();

        // Assert
        fade.IsRunning.Should().BeFalse();
        fixture.Gamma.Applied.Should().ContainSingle();
    }

    [Fact]
    public void RepairGammaDrift_FadeOffRejected_RetriesNeutralThenStopsMonitoring()
    {
        // Arrange
        using var fixture = new ControllerFixture(1000);
        fixture.Controller.UpdateSchedule(true);
        var drift = fixture.Runtime.Timers[1];
        fixture.Controller.Toggle();
        var fade = fixture.Runtime.Timers[^1];
        var runningDuringFade = drift.IsRunning;
        fixture.Gamma.ApplyResults.Enqueue(false);
        fixture.Runtime.Elapsed = TimeSpan.FromMilliseconds(500);
        fade.Fire();
        var runningAfterRejection = drift.IsRunning;
        fixture.Events.Clear();

        // Act
        drift.Fire();

        // Assert
        runningDuringFade.Should().BeFalse();
        runningAfterRejection.Should().BeTrue();
        fixture.Events.Should().Equal("apply");
        fixture.Gamma.Applied[^1].Should().Be((6500d, 100d));
        drift.IsRunning.Should().BeFalse();
    }

    internal sealed class ControllerFixture : IDisposable
    {
        public ControllerFixture(int fadeMilliseconds = 0)
        {
            Gamma = new FakeGamma(Events);
            Runtime = new FakeRuntime(Events);
            Controller = new AppController(
                () => Gamma,
                Runtime,
                () => Events.Add("shutdown"),
                new AppSettings
                {
                    Temperature = 3400,
                    Brightness = 75,
                    Mode = ScheduleMode.Custom,
                    FadeMs = fadeMilliseconds
                });
        }

        public List<string> Events { get; } = [];

        public FakeGamma Gamma { get; }

        public FakeRuntime Runtime { get; }

        public AppController Controller { get; }

        public void Dispose()
        {
            Controller.Dispose();
            Gamma.Dispose();
        }
    }

    internal sealed class FakeGamma(List<string> events) : IGammaService
    {
        private bool _disposed;

        public List<(double Kelvin, double Brightness)> Applied { get; } = [];

        public Queue<bool> ApplyResults { get; } = new();

        public Exception? ApplyException { get; set; }

        public Exception? ResetException { get; set; }

        public Exception? OpenException { get; set; }

        public Exception? RepairException { get; set; }

        public Exception? ResetAllException { get; set; }

        public bool ResetResult { get; set; } = true;

        public void OpenDisplays()
        {
            events.Add("open");
            if (OpenException is not null)
            {
                throw OpenException;
            }
        }

        public bool Apply(double kelvin, double brightnessPercent)
        {
            events.Add("apply");
            Applied.Add((kelvin, brightnessPercent));
            if (ApplyException is not null)
            {
                throw ApplyException;
            }

            return !ApplyResults.TryDequeue(out var result) || result;
        }

        public bool Reset()
        {
            events.Add("reset");
            if (ResetException is not null)
            {
                throw ResetException;
            }

            return ResetResult;
        }

        public void RepairDrift()
        {
            events.Add("repair");
            if (RepairException is not null)
            {
                throw RepairException;
            }
        }

        public void ResetAll()
        {
            events.Add("reset-all");
            if (ResetAllException is not null)
            {
                throw ResetAllException;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                events.Add("dispose");
                _disposed = true;
            }
        }
    }

    internal sealed class FakeRuntime(List<string> events) : IAppControllerRuntime
    {
        public DateTime Now { get; set; } = new(2026, 9, 4, 22, 0, 0, DateTimeKind.Local);

        public DateTime UtcNow => Now;

        public bool IsStartupEnabled => false;

        public TimeSpan Elapsed { get; set; }

        public List<FakeTimer> Timers { get; } = [];

        public Queue<Action> Posted { get; } = new();

        public AppSettings? Saved { get; private set; }

        public Exception? SaveException { get; set; }

        public Exception? StartupException { get; set; }

        public Exception? InvokeException { get; set; }

        public bool HasAccess { get; set; } = true;

        public long GetTimestamp()
        {
            return Elapsed.Ticks;
        }

        public TimeSpan GetElapsedTime(long timestamp)
        {
            return Elapsed - TimeSpan.FromTicks(timestamp);
        }

        public SettingsLoadResult LoadSettings()
        {
            throw new NotSupportedException("Tests must not initialize live UI.");
        }

        public void SaveSettings(AppSettings settings)
        {
            events.Add("save");
            if (SaveException is not null)
            {
                throw SaveException;
            }

            Saved = settings;
        }

        public void SetStartupEnabled(bool enabled)
        {
            events.Add($"startup:{enabled}");
            if (StartupException is not null)
            {
                throw StartupException;
            }
        }

        public IControllerTimer CreateTimer(TimeSpan interval, EventHandler handler)
        {
            var timer = new FakeTimer(interval, handler);
            Timers.Add(timer);
            return timer;
        }

        public void Post(Action action)
        {
            Posted.Enqueue(action);
        }

        public bool CheckAccess()
        {
            return HasAccess;
        }

        public void Invoke(Action action)
        {
            events.Add("invoke");
            if (InvokeException is not null)
            {
                throw InvokeException;
            }

            action();
        }

        public void DrainPosted()
        {
            while (Posted.TryDequeue(out var action))
            {
                action();
            }
        }

        public void RefreshTimeZone()
        {
            events.Add("refresh-tz");
        }
    }

    internal sealed class FakeTimer(TimeSpan interval, EventHandler handler) : IControllerTimer
    {
        public TimeSpan Interval { get; } = interval;

        public bool IsRunning { get; private set; }

        public int StartCount { get; private set; }

        public void Start()
        {
            StartCount++;
            IsRunning = true;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        public void Fire()
        {
            if (IsRunning)
            {
                handler(this, EventArgs.Empty);
            }
        }
    }
}
