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
            () => signaledHandle,
            () =>
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
            WatchdogService.Run(() => throw failure, () => orderly, () => resets++);
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
            () =>
            {
                orderly = true;
                return 1;
            },
            () => orderly,
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
            () => throw new IOException("unrelated"),
            () =>
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
        var run = () => WatchdogService.Run(() => 1, () => false, () => throw failure);

        // Assert
        run.Should().Throw<IOException>().WithMessage("recovery failed");
    }

    [Fact]
    public void TryParseArguments_ValidArguments_ReturnsParsedValues()
    {
        // Arrange
        const string eventName = @"Local\NightEmber.Watchdog.0123456789abcdef0123456789abcdef";
        string[] arguments = ["--watchdog", "42", eventName];

        // Act
        var result = WatchdogService.TryParseArguments(arguments, out var processId, out var parsedEventName);

        // Assert
        result.Should().BeTrue();
        processId.Should().Be(42);
        parsedEventName.Should().Be(eventName);
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
