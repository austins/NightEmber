using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class WatchdogServiceTests
{
    [Theory]
    [InlineData(0, false, 0, 0)]
    [InlineData(0, true, 0, 0)]
    [InlineData(1, true, 1, 0)]
    [InlineData(1, false, 1, 1)]
    public void Run_WaitResult_RechecksOrderlyExitBeforeRecovery(
        int signaledHandle,
        bool orderly,
        int expectedChecks,
        int expectedResets)
    {
        // Arrange
        var checks = 0;
        var resets = 0;

        // Act
        WatchdogService.Run(
            0,
            _ => signaledHandle,
            _ =>
            {
                checks++;
                return orderly;
            },
            () => resets++);

        // Assert
        checks.Should().Be(expectedChecks);
        resets.Should().Be(expectedResets);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ParentDisappearsDuringMonitoring_RespectsOrderlyExit(bool orderly)
    {
        // Arrange
        Exception[] failures =
        [
            new ArgumentException("parent no longer exists"),
            new InvalidOperationException(),
            new System.ComponentModel.Win32Exception()
        ];
        var resetCounts = new List<int>();

        // Act
        foreach (var failure in failures)
        {
            var resets = 0;
            WatchdogService.Run(0, _ => throw failure, _ => orderly, () => resets++);
            resetCounts.Add(resets);
        }

        // Assert
        resetCounts.Should().AllSatisfy(resets => resets.Should().Be(orderly ? 0 : 1));
    }

    [Fact]
    public void Run_OrderlySignalArrivesWithParentExit_SuppressesRecovery()
    {
        // Arrange
        var orderly = false;
        var resets = 0;

        // Act
        WatchdogService.Run(
            0,
            _ =>
            {
                orderly = true;
                return 1;
            },
            _ => orderly,
            () => resets++);

        // Assert
        resets.Should().Be(0);
    }

    [Fact]
    public void Run_UnrelatedMonitoringFailure_PropagatesWithoutRecovery()
    {
        // Arrange
        var resets = 0;
        var checks = 0;

        // Act
        var run = () => WatchdogService.Run(
            0,
            _ => throw new IOException("unrelated"),
            _ =>
            {
                checks++;
                return false;
            },
            () => resets++);

        // Assert
        run.Should().Throw<IOException>().WithMessage("unrelated");
        resets.Should().Be(0);
        checks.Should().Be(0);
    }

    [Fact]
    public void Run_UnexpectedExit_RecoveryFailurePropagates()
    {
        // Arrange
        var failure = new IOException("recovery failed");

        // Act
        var run = () => WatchdogService.Run(0, _ => 1, _ => false, () => throw failure);

        // Assert
        run.Should().Throw<IOException>().WithMessage("recovery failed");
    }

    [Fact]
    public void Run_UnexpectedExitAndNoDisplays_AttemptsRecoveryOnceWithoutThrowing()
    {
        // Arrange
        var resets = 0;

        // Act
        var run = () => WatchdogService.Run(
            0,
            _ => 1,
            _ => false,
            () =>
            {
                resets++;
                throw new System.ComponentModel.Win32Exception("no displays");
            });

        // Assert
        run.Should().NotThrow();
        resets.Should().Be(1);
    }

    [Theory]
    [InlineData(new[] { "--watchdog" }, true)]
    [InlineData(new[] { "--watchdog", "1", "x" }, true)]
    [InlineData(new[] { "--watchdog-launcher", "1", "x" }, true)]
    [InlineData(new[] { "--WATCHDOG" }, false)]
    [InlineData(new[] { "--hidden" }, false)]
    [InlineData(new string[0], false)]
    public void IsWatchdogCommand_Arguments_MatchesOnlyExactFirstSwitch(string[] arguments, bool expected)
    {
        // Act
        var result = WatchdogService.IsWatchdogCommand(arguments);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void RunFromCommandLine_MalformedArguments_ReturnsErrorWithoutMonitoring()
    {
        // Arrange
        var runs = 0;
        var resets = 0;

        // Act
        var exitCode = WatchdogService.RunFromCommandLine(
            ["--watchdog", "not-a-pid"],
            (_, _) => runs++,
            () => resets++,
            (_, _) => runs++);

        // Assert
        exitCode.Should().NotBe(0);
        runs.Should().Be(0);
        resets.Should().Be(0);
    }

    [Fact]
    public void RunFromCommandLine_ValidArguments_MonitorsParsedProcess()
    {
        // Arrange
        const string eventName = @"Local\NightEmber.Watchdog.0123456789abcdef0123456789abcdef";
        (int ProcessId, string EventName)? monitored = null;

        // Act
        var exitCode = WatchdogService.RunFromCommandLine(
            ["--watchdog", "42", eventName],
            (processId, name) => monitored = (processId, name),
            static () => throw new InvalidOperationException("unexpected reset"),
            static (_, _) => throw new InvalidOperationException("unexpected launch"));

        // Assert
        exitCode.Should().Be(0);
        monitored.Should().Be((42, eventName));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunFromCommandLine_OrderlyExitEventMissing_ResetsWithoutThrowing(bool resetFails)
    {
        // Arrange
        const string eventName = @"Local\NightEmber.Watchdog.0123456789abcdef0123456789abcdef";
        var resets = 0;

        // Act
        var exitCode = WatchdogService.RunFromCommandLine(
            ["--watchdog", "42", eventName],
            static (_, _) => throw new WaitHandleCannotBeOpenedException(),
            () =>
            {
                resets++;
                if (resetFails)
                {
                    throw new System.ComponentModel.Win32Exception("no displays");
                }
            },
            static (_, _) => throw new InvalidOperationException("unexpected launch"));

        // Assert
        exitCode.Should().Be(0);
        resets.Should().Be(1);
    }

    [Theory]
    [InlineData("--watchdog")]
    [InlineData("--watchdog-launcher")]
    public void TryParseArguments_ValidArguments_ReturnsParsedValues(string mode)
    {
        // Arrange
        const string eventName = @"Local\NightEmber.Watchdog.0123456789abcdef0123456789abcdef";
        string[] arguments = [mode, "42", eventName];

        // Act
        var result = WatchdogService.TryParseArguments(arguments, out var processId, out var parsedEventName);

        // Assert
        result.Should().BeTrue();
        processId.Should().Be(42);
        parsedEventName.Should().Be(eventName);
    }

    [Fact]
    public void RunFromCommandLine_Launcher_StartsWatchdogAndExitsWithoutMonitoring()
    {
        // Arrange
        const string eventName = @"Local\NightEmber.Watchdog.0123456789abcdef0123456789abcdef";
        (int ProcessId, string EventName)? launched = null;
        var runs = 0;

        // Act
        var exitCode = WatchdogService.RunFromCommandLine(
            ["--watchdog-launcher", "42", eventName],
            (_, _) => runs++,
            static () => throw new InvalidOperationException("unexpected reset"),
            (processId, name) => launched = (processId, name));

        // Assert
        exitCode.Should().Be(0);
        runs.Should().Be(0);
        launched.Should().Be((42, eventName));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RunFromCommandLine_LauncherCannotStartWatchdog_ReturnsFailureWithoutReset(bool win32Failure)
    {
        // Arrange
        const string eventName = @"Local\NightEmber.Watchdog.0123456789abcdef0123456789abcdef";
        Exception failure = win32Failure
            ? new System.ComponentModel.Win32Exception("access denied")
            : new InvalidOperationException("no path");
        var resets = 0;

        // Act
        var exitCode = WatchdogService.RunFromCommandLine(
            ["--watchdog-launcher", "42", eventName],
            static (_, _) => throw new InvalidOperationException("unexpected run"),
            () => resets++,
            (_, _) => throw failure);

        // Assert
        exitCode.Should().NotBe(0);
        resets.Should().Be(0);
    }

    [Fact]
    public void TryParseArguments_InvalidArguments_ReturnsFalseAndClearsOutputs()
    {
        // Arrange
        const string validEvent = @"Local\NightEmber.Watchdog.0123456789abcdef0123456789abcdef";
        string[][] invalidArguments =
        [
            [],
            ["--watchdog"],
            ["--watchdog", "42"],
            ["--watchdog", "42", validEvent, "extra"],
            ["--WATCHDOG", "42", validEvent],
            ["--watchdog-launcher", "42"],
            ["watchdog", "42", validEvent],
            ["--watchdog", "0", validEvent],
            ["--watchdog", "-1", validEvent],
            ["--watchdog", "+42", validEvent],
            ["--watchdog", " 42", validEvent],
            ["--watchdog", "not-a-number", validEvent],
            ["--watchdog", "42", @"Local\NightEmber.Other.0123456789abcdef0123456789abcdef"],
            ["--watchdog", "42", @"Local\NightEmber.Watchdog.not-a-guid"],
            ["--watchdog", "42", @"Local\NightEmber.Watchdog.01234567-89ab-cdef-0123-456789abcdef"]
        ];

        // Act
        var results = invalidArguments
            .Select(arguments =>
            {
                var result = WatchdogService.TryParseArguments(arguments, out var processId, out var eventName);
                return (Result: result, ProcessId: processId, EventName: eventName);
            })
            .ToArray();

        // Assert
        foreach (var result in results)
        {
            result.Result.Should().BeFalse();
            result.ProcessId.Should().Be(0);
            result.EventName.Should().BeEmpty();
        }
    }

    [Fact]
    public void IsProcessMonitoringException_KnownProcessFailures_ReturnsTrue()
    {
        // Arrange
        Exception[] exceptions =
        [
            new ArgumentException(), new InvalidOperationException(), new System.ComponentModel.Win32Exception()
        ];

        // Act
        var results = exceptions.Select(WatchdogService.IsProcessMonitoringException);

        // Assert
        results.Should().AllSatisfy(static result => result.Should().BeTrue());
    }

    [Fact]
    public void IsProcessMonitoringException_UnrelatedFailure_ReturnsFalse()
    {
        // Arrange
        var exception = new IOException();

        // Act
        var result = WatchdogService.IsProcessMonitoringException(exception);

        // Assert
        result.Should().BeFalse();
    }
}
