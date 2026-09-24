using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class CrashLogTests : IDisposable
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 4, 22, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Directory.CreateTempSubdirectory("NightEmber.CrashLogTests.").FullName;

    private string LogPath => Path.Combine(_directory, "NightEmber.log");

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void Write_RepeatedExceptions_AppendsTimestampedEntries()
    {
        // Act
        CrashLog.Write(LogPath, new InvalidOperationException("first"), Timestamp);
        CrashLog.Write(LogPath, new InvalidOperationException("second"), Timestamp.AddMinutes(1));

        // Assert
        var text = File.ReadAllText(LogPath);
        text.Should().Contain("[2026-09-04T22:00:00.0000000+00:00] Unhandled exception");
        text.Should().Contain("first");
        text.Should().Contain("[2026-09-04T22:01:00.0000000+00:00] Unhandled exception");
        text.Should().Contain("second");
    }

    [Fact]
    public void Write_OversizedLog_StartsNewFile()
    {
        // Arrange
        File.WriteAllText(LogPath, new string('x', (int)CrashLog.MaximumBytes));

        // Act
        CrashLog.Write(LogPath, new InvalidOperationException("latest"), Timestamp);

        // Assert
        var text = File.ReadAllText(LogPath);
        text.Should().NotContain("xxx");
        text.Should().Contain("latest");
    }

    [Fact]
    public void Write_UnwritableLocation_DoesNotThrow()
    {
        // Arrange
        var path = Path.Combine(_directory, "missing", "NightEmber.log");

        // Act
        var write = () => CrashLog.Write(path, new InvalidOperationException("lost"), Timestamp);

        // Assert
        write.Should().NotThrow();
    }
}
