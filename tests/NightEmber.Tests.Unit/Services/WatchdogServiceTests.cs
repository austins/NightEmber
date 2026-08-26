using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class WatchdogServiceTests
{
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
            new ArgumentException(),
            new InvalidOperationException(),
            new System.ComponentModel.Win32Exception()
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
