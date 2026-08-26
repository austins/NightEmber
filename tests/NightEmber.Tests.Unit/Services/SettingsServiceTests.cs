using NightEmber.Models;
using System.Text.Json;
using ProductionSettingsService = NightEmber.Services.SettingsService;

namespace NightEmber.Tests.Unit.Services;

public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _configPath;
    private readonly string _directory;
    private readonly ProductionSettingsService _service;

    public SettingsServiceTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "NightEmber.Tests.Unit", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _configPath = Path.Combine(_directory, "settings.json");
        _service = new ProductionSettingsService(_configPath);
    }

    [Fact]
    public void Load_MissingFile_ReturnsDefaultsAsFirstRun()
    {
        // Act
        var result = _service.Load();

        // Assert
        result.Settings.Should().BeEquivalentTo(AppSettings.Default);
        result.IsFirstRun.Should().BeTrue();
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void Load_ValidFile_ReturnsEveryPersistedValue()
    {
        // Arrange
        File.WriteAllText(
            _configPath,
            """
            {
              "Temperature": 2400,
              "Brightness": 80,
              "Mode": "Custom",
              "CustomOn": "19:30",
              "CustomOff": "05:45",
              "FadeMs": 950
            }
            """);

        // Act
        var result = _service.Load();

        // Assert
        result.Settings.Should().BeEquivalentTo(
            new AppSettings
            {
                Temperature = 2400,
                Brightness = 80,
                Mode = ScheduleMode.Custom,
                CustomOn = "19:30",
                CustomOff = "05:45",
                FadeMs = 950
            });
        result.IsFirstRun.Should().BeFalse();
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void Load_OutOfRangeValues_ReturnsNormalizedSettings()
    {
        // Arrange
        File.WriteAllText(
            _configPath,
            """
            {
              "Temperature": 999,
              "Brightness": 101,
              "Mode": 999,
              "CustomOn": "25:00",
              "CustomOff": "invalid",
              "FadeMs": -1
            }
            """);

        // Act
        var result = _service.Load();

        // Assert
        result.Settings.Should().BeEquivalentTo(AppSettings.Default);
        result.IsFirstRun.Should().BeFalse();
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void Load_JsonNull_ReturnsDefaultsAndEmptyFileWarning()
    {
        // Arrange
        File.WriteAllText(_configPath, "null");

        // Act
        var result = _service.Load();

        // Assert
        result.Settings.Should().BeEquivalentTo(AppSettings.Default);
        result.IsFirstRun.Should().BeFalse();
        result.Warning.Should().StartWith("The settings file was empty.");
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"Mode":"Unknown"}""")]
    [InlineData("[]")]
    public void Load_InvalidJson_ReturnsDefaultsAndInvalidFileWarning(string json)
    {
        // Arrange
        File.WriteAllText(_configPath, json);

        // Act
        var result = _service.Load();

        // Assert
        result.Settings.Should().BeEquivalentTo(AppSettings.Default);
        result.IsFirstRun.Should().BeFalse();
        result.Warning.Should().StartWith("The settings file is invalid.");
    }

    [Fact]
    public void Load_LockedFile_ReturnsDefaultsAndReadWarning()
    {
        // Arrange
        File.WriteAllText(_configPath, "{}");
        using var lockStream = new FileStream(_configPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act
        var result = _service.Load();

        // Assert
        result.Settings.Should().BeEquivalentTo(AppSettings.Default);
        result.IsFirstRun.Should().BeFalse();
        result.Warning.Should().StartWith("The settings file could not be read.");
    }

    [Fact]
    public void Save_ValidSettings_WritesIndentedPortableJsonAndRoundTrips()
    {
        // Arrange
        var settings = new AppSettings
        {
            Temperature = 1800,
            Brightness = 65,
            Mode = ScheduleMode.Manual,
            CustomOn = "20:15",
            CustomOff = "08:30",
            FadeMs = 1500
        };

        // Act
        _service.Save(settings);
        var json = File.ReadAllText(_configPath);
        using var document = JsonDocument.Parse(json);
        var mode = document.RootElement.GetProperty("Mode").GetString();
        var loadedSettings = _service.Load().Settings;
        var temporaryFileExists = File.Exists(TemporaryPath());

        // Assert
        mode.Should().Be("Manual");
        json.Should().Contain("\n  \"Temperature\"");
        loadedSettings.Should().BeEquivalentTo(settings);
        temporaryFileExists.Should().BeFalse();
    }

    [Fact]
    public void Save_InvalidSettings_PersistsNormalizedDefaults()
    {
        // Arrange
        var settings = new AppSettings
        {
            Temperature = 0,
            Brightness = 0,
            Mode = (ScheduleMode)999,
            CustomOn = string.Empty,
            CustomOff = string.Empty,
            FadeMs = int.MaxValue
        };

        // Act
        _service.Save(settings);
        var loadedSettings = _service.Load().Settings;

        // Assert
        loadedSettings.Should().BeEquivalentTo(AppSettings.Default);
    }

    [Fact]
    public void Save_ExistingFile_ReplacesItsContents()
    {
        // Arrange
        File.WriteAllText(_configPath, "old content");
        var settings = new AppSettings { Temperature = 4200 };

        // Act
        _service.Save(settings);
        var loadedSettings = _service.Load().Settings;

        // Assert
        loadedSettings.Should().BeEquivalentTo(settings);
    }

    [Fact]
    public void Save_MoveFails_RemovesTemporaryFile()
    {
        // Arrange
        Directory.CreateDirectory(_configPath);

        // Act
        var exception = Record.Exception(() => _service.Save(AppSettings.Default));
        var temporaryFileExists = File.Exists(TemporaryPath());

        // Assert
        exception.Should().NotBeNull();
        temporaryFileExists.Should().BeFalse();
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    private string TemporaryPath()
    {
        return _configPath + $".{Environment.ProcessId}.tmp";
    }
}
