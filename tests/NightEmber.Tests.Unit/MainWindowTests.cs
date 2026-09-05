using NightEmber.Controls;
using NightEmber.Models;
using NightEmber.Tests.Unit.Services;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace NightEmber.Tests.Unit;

public sealed class MainWindowTests
{
    [Theory]
    [InlineData(Key.Down, FlowDirection.LeftToRight, "ManualModeRadio")]
    [InlineData(Key.Up, FlowDirection.LeftToRight, "SunsetModeRadio")]
    [InlineData(Key.Right, FlowDirection.LeftToRight, "ManualModeRadio")]
    [InlineData(Key.Left, FlowDirection.LeftToRight, "SunsetModeRadio")]
    [InlineData(Key.Left, FlowDirection.RightToLeft, "ManualModeRadio")]
    [InlineData(Key.Right, FlowDirection.RightToLeft, "SunsetModeRadio")]
    public async Task ScheduleArrowKey_SelectsAndWrapsAccordingToFlowDirection(
        Key key,
        FlowDirection flowDirection,
        string expectedRadio)
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            window.FlowDirection = flowDirection;
            var current = (RadioButton)window.FindName("CustomModeRadio");
            var expected = (RadioButton)window.FindName(expectedRadio);

            // Act
            var args = WpfTestHelper.PressKey(current, key);

            // Assert
            args.Handled.Should().BeTrue();
            expected.IsChecked.Should().BeTrue();
            current.IsChecked.Should().BeFalse();
            ((Grid)window.FindName("CustomTimePanel")).IsEnabled.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(Key.Down, ModifierKeys.Control)]
    [InlineData(Key.Up, ModifierKeys.Shift)]
    [InlineData(Key.Right, ModifierKeys.Alt)]
    [InlineData(Key.Tab, ModifierKeys.None)]
    [InlineData(Key.Home, ModifierKeys.None)]
    public async Task ScheduleKey_ModifiedOrUnrelated_DoesNotChangeTheSelection(Key key, ModifierKeys modifiers)
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            var current = (RadioButton)window.FindName("CustomModeRadio");

            // Act
            var args = WpfTestHelper.PressKey(current, key, modifiers);

            // Assert
            args.Handled.Should().BeFalse();
            current.IsChecked.Should().BeTrue();
            ((Grid)window.FindName("CustomTimePanel")).IsEnabled.Should().BeTrue();
        });
    }

    [Fact]
    public async Task Cancel_AfterSliderPreview_RestoresSavedGammaWithoutSaving()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var strength = (Slider)window.FindName("StrengthSlider");
            var brightness = (Slider)window.FindName("BrightnessSlider");
            var footer = (Grid)((Grid)window.Content).Children[1];
            var buttons = footer.Children.OfType<StackPanel>().Single();
            var cancel = buttons.Children.OfType<Button>().Single(static button => button.IsCancel);

            // Act
            strength.Value = 40;
            brightness.Value = 80;
            var preview = fixture.Gamma.Applied[^1];
            cancel.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            // Assert
            preview.Should().Be((MainWindow.StrengthToTemperature(40), 80d));
            fixture.Gamma.Applied[^1].Should().Be((3400d, 75d));
            fixture.Runtime.Saved.Should().BeNull();
        });
    }

    [Theory]
    [InlineData("CustomOnInput", "1212:00 PM", "CustomOnErrorText")]
    [InlineData("CustomOffInput", "", "CustomOffErrorText")]
    [InlineData("FadeDurationInput", "5001", "FadeDurationErrorText")]
    [InlineData("FadeDurationInput", "", "FadeDurationErrorText")]
    [InlineData("FadeDurationInput", "99999999999999", "FadeDurationErrorText")]
    public async Task Save_InvalidInput_PreservesTextShowsInlineErrorAndDoesNotSave(
        string inputName,
        string text,
        string errorName)
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var control = (UserControl)window.FindName(inputName);
            var input = (TextBox)control.FindName("ValueTextBox");
            input.Text = text;

            // Act
            Save(window);
            Save(window);

            // Assert
            fixture.Runtime.Saved.Should().BeNull();
            input.Text.Should().Be(text);
            InputValidation.GetHasError(control).Should().BeTrue();
            var error = (TextBlock)window.FindName(errorName);
            error.Text.Should().Contain(InputValidation.GetErrorMessage(control));
            error.Visibility.Should().Be(Visibility.Visible);
        });
    }

    [Fact]
    public async Task Save_MultipleInvalidInputs_ReportsEveryFieldThenSavesCorrections()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var on = (TimeUpDown)window.FindName("CustomOnInput");
            var off = (TimeUpDown)window.FindName("CustomOffInput");
            var duration = (NumericUpDown)window.FindName("FadeDurationInput");
            ((TextBox)on.FindName("ValueTextBox")).Text = "invalid";
            ((TextBox)off.FindName("ValueTextBox")).Text = "invalid";
            ((TextBox)duration.FindName("ValueTextBox")).Text = "5001";

            // Act
            Save(window);
            var errors = new[]
            {
                InputValidation.GetHasError(on),
                InputValidation.GetHasError(off),
                InputValidation.GetHasError(duration)
            };
            ((TextBox)on.FindName("ValueTextBox")).Text = "22:30";
            ((TextBox)off.FindName("ValueTextBox")).Text = "06:15";
            ((TextBox)duration.FindName("ValueTextBox")).Text = "5000";
            Save(window);

            // Assert
            errors.Should().AllBeEquivalentTo(true);
            fixture.Runtime.Saved.Should().NotBeNull();
            fixture.Runtime.Saved.CustomOn.Should().Be("22:30");
            fixture.Runtime.Saved.CustomOff.Should().Be("06:15");
            fixture.Runtime.Saved.FadeMs.Should().Be(5000);
        });
    }

    [Theory]
    [InlineData("ManualModeRadio", (int)ScheduleMode.Manual)]
    [InlineData("SunsetModeRadio", (int)ScheduleMode.Sunset)]
    public async Task Save_InactiveCustomInputs_DoesNotBlockOnUnusedInvalidEdits(string radioName, int mode)
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var on = (TimeUpDown)window.FindName("CustomOnInput");
            ((TextBox)on.FindName("ValueTextBox")).Text = "invalid";
            on.CommitEdit();
            ((RadioButton)window.FindName(radioName)).IsChecked = true;

            // Act
            Save(window);

            // Assert
            fixture.Runtime.Saved.Should().NotBeNull();
            fixture.Runtime.Saved.Mode.Should().Be((ScheduleMode)mode);
            fixture.Runtime.Saved.CustomOn.Should().Be("21:00");
            var error = (TextBlock)window.FindName("CustomOnErrorText");
            ((StackPanel)error.Parent).Visibility.Should().Be(Visibility.Collapsed);
        });
    }

    [Fact]
    public async Task Save_MatchingScheduleTimes_ShowsInlineErrorThatClearsWhenCorrected()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var off = (TimeUpDown)window.FindName("CustomOffInput");
            var input = (TextBox)off.FindName("ValueTextBox");
            input.Text = "21:00";

            // Act
            Save(window);
            var error = (TextBlock)window.FindName("ScheduleErrorText");
            var message = error.Text;
            var savedBeforeCorrection = fixture.Runtime.Saved;
            input.Text = "07:00";
            var correctedMessage = error.Text;
            Save(window);

            // Assert
            savedBeforeCorrection.Should().BeNull();
            message.Should().Contain("different");
            correctedMessage.Should().BeEmpty();
            fixture.Runtime.Saved.Should().NotBeNull();
            fixture.Runtime.Saved.CustomOff.Should().Be("07:00");
        });
    }

    [Theory]
    [InlineData(3400, 58, 3400)]
    [InlineData(3400, 58.4, 3400)]
    [InlineData(3400, 58.5, 3400)]
    [InlineData(3400, 58.6, 3373)]
    [InlineData(6500, -1, 6500)]
    [InlineData(1200, 101, 1200)]
    public void BuildSettings_Strength_PreservesUnchangedTemperatureOrRoundsAndClamps(
        int original,
        double strength,
        int expected)
    {
        // Arrange
        var off = TimeSpan.FromHours(7);

        // Act
        var settings = MainWindow.BuildSettings(original, strength, 80, ScheduleMode.Manual, TimeSpan.Zero, off, 300);

        // Assert
        settings.Temperature.Should().Be(expected);
    }

    [Theory]
    [InlineData((int)ScheduleMode.Manual, 80.5, 80)]
    [InlineData((int)ScheduleMode.Custom, 81.5, 82)]
    [InlineData((int)ScheduleMode.Sunset, 99.6, 100)]
    public void BuildSettings_SaveValues_PreservesModeFadeAndPortableTimes(
        int mode,
        double brightness,
        int expectedBrightness)
    {
        // Arrange
        var customOn = new TimeSpan(21, 35, 0);
        var customOff = new TimeSpan(7, 5, 0);

        // Act
        var settings = MainWindow.BuildSettings(3400, 58, brightness, (ScheduleMode)mode, customOn, customOff, 1234);

        // Assert
        settings.Brightness.Should().Be(expectedBrightness);
        settings.Mode.Should().Be((ScheduleMode)mode);
        settings.CustomOn.Should().Be("21:35");
        settings.CustomOff.Should().Be("07:05");
        settings.FadeMs.Should().Be(1234);
    }

    [Theory]
    [InlineData(6500, 0)]
    [InlineData(3400, 58)]
    [InlineData(1200, 100)]
    public void TemperatureToStrength_KnownTemperature_ReturnsExpectedStrength(int temperature, int expected)
    {
        // Act
        var result = MainWindow.TemperatureToStrength(temperature);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1, 6500)]
    [InlineData(0, 6500)]
    [InlineData(1, 6447)]
    [InlineData(50, 3850)]
    [InlineData(99, 1253)]
    [InlineData(100, 1200)]
    [InlineData(101, 1200)]
    public void StrengthToTemperature_Strength_ClampsAndConvertsTemperature(int strength, int expected)
    {
        // Act
        var result = MainWindow.StrengthToTemperature(strength);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, 21, 7, true)]
    [InlineData(true, 0, 1, true)]
    [InlineData(true, 7, 7, false)]
    [InlineData(false, 7, 7, true)]
    public void IsCustomWindowValid_ScheduleTimes_DetectsZeroLengthWindow(
        bool isCustomMode,
        int onHours,
        int offHours,
        bool expected)
    {
        // Act
        var result = MainWindow.IsCustomWindowValid(
            isCustomMode,
            TimeSpan.FromHours(onHours),
            TimeSpan.FromHours(offHours));

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void IsCustomWindowValid_TimesWithinSameMinute_RejectsIdenticalPersistedTimes()
    {
        // Arrange
        var on = new TimeSpan(21, 0, 15);
        var off = new TimeSpan(21, 0, 45);

        // Act
        var result = MainWindow.IsCustomWindowValid(true, on, off);

        // Assert
        result.Should().BeFalse();
    }

    private static Task WithWindow(Action<MainWindow, AppControllerTests.ControllerFixture> action)
    {
        return WpfTestHelper.RunAsync(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            using var fixture = new AppControllerTests.ControllerFixture();
            var window = new MainWindow(fixture.Controller);
            try
            {
                action(window, fixture);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Save(MainWindow window)
    {
        ((Button)window.FindName("SaveButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
    }
}
