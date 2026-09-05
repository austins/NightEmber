using NightEmber.Models;
using NightEmber.Services;
using System.Globalization;
using System.IO;
using System.Windows;

namespace NightEmber;

/// <summary>
/// Provides the settings window for configuring and previewing Night Ember.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int MinimumStrength = 0;
    private const int MaximumStrength = 100;

    private const double KelvinPerStrengthPoint =
        (AppSettings.MaximumTemperature - AppSettings.MinimumTemperature) / (double)MaximumStrength;

    private readonly bool _initialized;
    private readonly AppController _controller;
    private int _originalTemperature;
    private bool _saved;

    /// <summary>
    /// Initializes the settings window from the active application state.
    /// </summary>
    /// <param name="controller">The application controller that owns settings and previews.</param>
    internal MainWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent();
        LoadSettings(controller.CurrentSettings);
        Closing += MainWindow_Closing;
        _initialized = true;
        UpdatePreviewLabels();
        UpdateScheduleControls();
    }

    internal static int TemperatureToStrength(int temperature)
    {
        return (int)Math.Round((AppSettings.MaximumTemperature - temperature) / KelvinPerStrengthPoint);
    }

    internal static int StrengthToTemperature(int strength)
    {
        return (int)Math.Round(
            AppSettings.MaximumTemperature
            - Math.Clamp(strength, MinimumStrength, MaximumStrength) * KelvinPerStrengthPoint);
    }

    /// <summary>
    /// Determines whether a custom schedule can ever change state.
    /// </summary>
    /// <param name="isCustomMode">Whether the custom schedule is selected.</param>
    /// <param name="on">The configured activation time.</param>
    /// <param name="off">The configured deactivation time.</param>
    /// <returns><see langword="true" /> when the schedule is usable.</returns>
    /// <remarks>
    /// Matching times describe a zero-length window, which never activates and
    /// never produces a next-change boundary.
    /// </remarks>
    internal static bool IsCustomWindowValid(bool isCustomMode, TimeSpan on, TimeSpan off)
    {
        return !isCustomMode || on != off;
    }

    internal static AppSettings BuildSettings(
        int originalTemperature,
        double strength,
        double brightness,
        ScheduleMode mode,
        TimeSpan customOn,
        TimeSpan customOff,
        int fadeMilliseconds)
    {
        var roundedStrength = (int)Math.Round(strength);
        return new AppSettings
        {
            Temperature =
                roundedStrength == TemperatureToStrength(originalTemperature)
                    ? originalTemperature
                    : StrengthToTemperature(roundedStrength),
            Brightness = (int)Math.Round(brightness),
            Mode = mode,
            CustomOn = AppSettings.FormatTime(customOn),
            CustomOff = AppSettings.FormatTime(customOff),
            FadeMs = fadeMilliseconds
        };
    }

    private void LoadSettings(AppSettings settings)
    {
        StrengthSlider.Value = TemperatureToStrength(settings.Temperature);
        _originalTemperature = settings.Temperature;
        BrightnessSlider.Value = settings.Brightness;

        ManualModeRadio.IsChecked = settings.Mode == ScheduleMode.Manual;
        SunsetModeRadio.IsChecked = settings.Mode == ScheduleMode.Sunset;
        CustomModeRadio.IsChecked = settings.Mode == ScheduleMode.Custom;

        if (!AppSettings.TryParseTime(settings.CustomOn, out var customOn))
        {
            customOn = TimeSpan.Zero;
        }

        if (!AppSettings.TryParseTime(settings.CustomOff, out var customOff))
        {
            customOff = TimeSpan.Zero;
        }

        CustomOnInput.TimeValue = customOn;
        CustomOffInput.TimeValue = customOff;
        FadeDurationInput.NumericValue = settings.FadeMs;
        StartupCheckBox.IsChecked = _controller.IsStartupEnabled;

        var solar = AppController.GetSolarSummary();
        var timePattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
        SunInfoText.Text = solar.Sunrise is not null && solar.Sunset is not null
            ? $"Today: sunset {solar.Sunset.Value.ToString(timePattern, CultureInfo.CurrentCulture)}, "
              + $"sunrise {solar.Sunrise.Value.ToString(timePattern, CultureInfo.CurrentCulture)}\n"
              + $"Estimated from time zone: {solar.Source}"
            : "The sun does not rise or set here today.";
    }

    private void PreviewSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_initialized)
        {
            return;
        }

        UpdatePreviewLabels();
        _controller.Preview(
            StrengthToTemperature((int)Math.Round(StrengthSlider.Value)),
            (int)Math.Round(BrightnessSlider.Value));
    }

    private void ScheduleMode_Checked(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            UpdateScheduleControls();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CustomOnInput.CommitEdit() || !CustomOffInput.CommitEdit())
        {
            MessageBox.Show(
                $"Enter both times using your Windows time format, for example "
                + $"{DateTime.Today.AddHours(21).ToString(CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern, CultureInfo.CurrentCulture)}.",
                "Night Ember",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        FadeDurationInput.CommitEdit();

        if (!IsCustomWindowValid(CustomModeRadio.IsChecked == true, CustomOnInput.TimeValue, CustomOffInput.TimeValue))
        {
            MessageBox.Show(
                "Set different turn-on and turn-off times. Night Ember never changes state when both times match.",
                "Night Ember",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var settings = BuildSettings(
            _originalTemperature,
            StrengthSlider.Value,
            BrightnessSlider.Value,
            SunsetModeRadio.IsChecked == true ? ScheduleMode.Sunset :
            CustomModeRadio.IsChecked == true ? ScheduleMode.Custom : ScheduleMode.Manual,
            CustomOnInput.TimeValue,
            CustomOffInput.TimeValue,
            FadeDurationInput.NumericValue);

        try
        {
            var warning = _controller.SaveSettings(settings, StartupCheckBox.IsChecked == true);
            _saved = true;
            Close();
            if (warning is not null)
            {
                MessageBox.Show(warning, "Night Ember", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or System.Security.SecurityException)
        {
            MessageBox.Show(
                $"The settings could not be saved.\n\n{exception.Message}",
                "Night Ember",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_saved)
        {
            _controller.CancelPreview();
        }
    }

    private void UpdatePreviewLabels()
    {
        var strength = (int)Math.Round(StrengthSlider.Value);
        var temperature = StrengthToTemperature(strength);
        var brightness = (int)Math.Round(BrightnessSlider.Value);
        StrengthValueText.Text = $"{strength}% ({temperature} K)";
        BrightnessValueText.Text = brightness == 100 ? "100% (no dimming)" : $"{brightness}%";
    }

    private void UpdateScheduleControls()
    {
        CustomTimePanel.IsEnabled = CustomModeRadio.IsChecked == true;
    }
}
