using NightEmber.Models;
using System.IO;
using System.Text.Json;

namespace NightEmber.Services;

/// <summary>
/// Loads and atomically saves the portable JSON configuration beside the executable.
/// </summary>
internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsService" /> class.
    /// </summary>
    public SettingsService()
        : this(Path.Combine(AppContext.BaseDirectory, "NightEmber.config.json"))
    {
    }

    public SettingsService(string configPath)
    {
        ConfigPath = configPath;
    }

    /// <summary>
    /// Gets the full path to the portable configuration file.
    /// </summary>
    private string ConfigPath { get; }

    /// <summary>
    /// Loads and normalizes the portable configuration.
    /// </summary>
    /// <returns>
    /// The loaded settings, first-run state, and any recoverable warning to display.
    /// </returns>
    /// <remarks>
    /// Missing files are treated as a first run. Malformed or inaccessible files
    /// return safe defaults with a warning rather than preventing startup.
    /// </remarks>
    public SettingsLoadResult Load()
    {
        if (!File.Exists(ConfigPath))
        {
            return new SettingsLoadResult(AppSettings.Default, true, null);
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
            if (settings is null)
            {
                return new SettingsLoadResult(
                    AppSettings.Default,
                    false,
                    "The settings file was empty. Safe defaults have been loaded.");
            }

            return new SettingsLoadResult(settings.Normalize(), false, null);
        }
        catch (JsonException exception)
        {
            return new SettingsLoadResult(
                AppSettings.Default,
                false,
                $"The settings file is invalid. Safe defaults have been loaded.\n\n{exception.Message}");
        }
        catch (IOException exception)
        {
            return new SettingsLoadResult(
                AppSettings.Default,
                false,
                $"The settings file could not be read. Safe defaults have been loaded.\n\n{exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new SettingsLoadResult(
                AppSettings.Default,
                false,
                $"The settings file could not be accessed. Safe defaults have been loaded.\n\n{exception.Message}");
        }
    }

    /// <summary>
    /// Normalizes and atomically saves the portable configuration.
    /// </summary>
    /// <param name="settings">The settings to persist.</param>
    /// <remarks>
    /// Data is written to a process-specific temporary file and moved over the
    /// destination to avoid leaving a partially written configuration.
    /// </remarks>
    public void Save(AppSettings settings)
    {
        var normalized = settings.Normalize();
        var json = JsonSerializer.Serialize(normalized, SerializerOptions);
        var temporaryPath = ConfigPath + $".{Environment.ProcessId}.tmp";

        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, ConfigPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

/// <summary>
/// Describes the result of loading portable settings.
/// </summary>
/// <param name="Settings">The normalized settings to use.</param>
/// <param name="IsFirstRun">Whether no configuration file existed.</param>
/// <param name="Warning">A recoverable warning suitable for display to the user.</param>
internal sealed record SettingsLoadResult(AppSettings Settings, bool IsFirstRun, string? Warning);
