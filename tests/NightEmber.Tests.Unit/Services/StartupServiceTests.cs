using NightEmber.Services;

namespace NightEmber.Tests.Unit.Services;

public sealed class StartupServiceTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("NightEmber.StartupServiceTests.").FullName;

    public StartupServiceTests()
    {
        ExecutablePath = CreateFile("current", "NightEmber.exe");
        OtherExecutablePath = CreateFile("other", "NightEmber.exe");
    }

    private string ShortcutPath => Path.Combine(_directory, "Night Ember.lnk");

    private string ExecutablePath { get; }

    private string OtherExecutablePath { get; }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    [Fact]
    public void SetEnabled_True_CreatesShortcutForThisExecutable()
    {
        // Arrange
        var service = new StartupService(ShortcutPath, ExecutablePath);

        // Act
        var enabledBefore = service.IsEnabled;
        service.SetEnabled(true);

        // Assert
        enabledBefore.Should().BeFalse();
        File.Exists(ShortcutPath).Should().BeTrue();
        service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SetEnabled_TrueWhenAlreadyCurrent_LeavesShortcutUntouched()
    {
        // Arrange
        var service = new StartupService(ShortcutPath, ExecutablePath);
        service.SetEnabled(true);
        var marker = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(ShortcutPath, marker);

        // Act
        service.SetEnabled(true);

        // Assert
        File.GetLastWriteTimeUtc(ShortcutPath).Should().Be(marker);
    }

    [Fact]
    public void SetEnabled_False_RemovesThisExecutablesShortcut()
    {
        // Arrange
        var service = new StartupService(ShortcutPath, ExecutablePath);
        service.SetEnabled(true);

        // Act
        service.SetEnabled(false);
        service.SetEnabled(false);

        // Assert
        File.Exists(ShortcutPath).Should().BeFalse();
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ShortcutForOtherExistingCopy_IsNotEnabledAndDisablingLeavesIt()
    {
        // Arrange
        new StartupService(ShortcutPath, OtherExecutablePath).SetEnabled(true);
        var service = new StartupService(ShortcutPath, ExecutablePath);

        // Act
        var enabled = service.IsEnabled;
        service.SetEnabled(false);

        // Assert
        enabled.Should().BeFalse();
        File.Exists(ShortcutPath).Should().BeTrue();
        new StartupService(ShortcutPath, OtherExecutablePath).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void SetEnabled_TrueOverOtherCopy_RetargetsShortcut()
    {
        // Arrange
        var other = new StartupService(ShortcutPath, OtherExecutablePath);
        other.SetEnabled(true);
        var service = new StartupService(ShortcutPath, ExecutablePath);

        // Act
        service.SetEnabled(true);

        // Assert
        service.IsEnabled.Should().BeTrue();
        other.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetEnabled_FalseWithShortcutToMissingExecutable_RemovesStaleShortcut()
    {
        // Arrange
        var movedPath = CreateFile("moved", "NightEmber.exe");
        new StartupService(ShortcutPath, movedPath).SetEnabled(true);
        File.Delete(movedPath);
        var service = new StartupService(ShortcutPath, ExecutablePath);

        // Act
        var enabled = service.IsEnabled;
        service.SetEnabled(false);

        // Assert
        enabled.Should().BeFalse();
        File.Exists(ShortcutPath).Should().BeFalse();
    }

    [Fact]
    public void SetEnabled_FalseWithUnreadableShortcut_RemovesIt()
    {
        // Arrange
        File.WriteAllText(ShortcutPath, "not a shortcut");
        var service = new StartupService(ShortcutPath, ExecutablePath);

        // Act
        var enabled = service.IsEnabled;
        service.SetEnabled(false);

        // Assert
        enabled.Should().BeFalse();
        File.Exists(ShortcutPath).Should().BeFalse();
    }

    private string CreateFile(string folder, string name)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_directory, folder)).FullName;
        var path = Path.Combine(directory, name);
        File.WriteAllBytes(path, []);
        return path;
    }
}
