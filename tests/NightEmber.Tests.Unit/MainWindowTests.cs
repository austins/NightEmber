using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using NightEmber.Controls;
using NightEmber.Core.Models;
using NightEmber.Tests.Unit.Services;
using System.Globalization;
using static NightEmber.Tests.Unit.Controls.SpinnerTestHelper;
using NumericUpDown = NightEmber.Controls.NumericUpDown;

namespace NightEmber.Tests.Unit;

public sealed class MainWindowTests
{
    [Fact]
    public async Task Show_InitialFocus_IsOnStrength()
    {
        await WithWindow((window, _) =>
        {
            // Assert
            Focused(window).Should().BeSameAs(Find<Slider>(window, "StrengthSlider"));
        });
    }

    [Fact]
    public async Task Show_DefaultSize_ShowsAllContentWithoutScrolling()
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            var scroller = window.GetVisualDescendants().OfType<ScrollViewer>().First();

            // Assert
            scroller.Extent.Height.Should().BeLessThanOrEqualTo(scroller.Viewport.Height);
            scroller.Extent.Width.Should().BeLessThanOrEqualTo(scroller.Viewport.Width);
        });
    }

    [Fact]
    public async Task Tab_CustomSchedule_VisitsEachControlOnceWithOneRadioStopAndCycles()
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            var expected = new object[]
            {
                Find<Slider>(window, "StrengthSlider"),
                Find<Slider>(window, "BrightnessSlider"),
                Find<RadioButton>(window, "CustomModeRadio"),
                Input(Find<TimeUpDown>(window, "CustomOnInput")),
                Input(Find<TimeUpDown>(window, "CustomOffInput")),
                Input(Find<NumericUpDown>(window, "FadeDurationInput")),
                Find<CheckBox>(window, "StartupCheckBox"),
                CancelButton(window),
                Find<Button>(window, "SaveButton"),
                Find<Slider>(window, "StrengthSlider")
            };

            // Act
            var forward = new List<object?>();
            for (var i = 1; i < expected.Length; i++)
            {
                AvaloniaTestHelper.PressKey(window, Key.Tab);
                forward.Add(Focused(window));
            }

            var backward = new List<object?>();
            for (var i = 1; i < expected.Length; i++)
            {
                AvaloniaTestHelper.PressKey(window, Key.Tab, KeyModifiers.Shift);
                backward.Add(Focused(window));
            }

            // Assert
            forward.Should().Equal(expected.Skip(1));
            backward.Should().Equal(expected.Reverse().Skip(1));
        });
    }

    [Theory]
    [InlineData("ManualModeRadio")]
    [InlineData("SunsetModeRadio")]
    public async Task Tab_NonCustomSchedule_SelectedRadioIsTheStopAndCustomFieldsAreSkipped(string radioName)
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            Find<RadioButton>(window, radioName).IsChecked = true;
            var expected = new object[]
            {
                Find<Slider>(window, "BrightnessSlider"),
                Find<RadioButton>(window, radioName),
                Input(Find<NumericUpDown>(window, "FadeDurationInput")),
                Find<CheckBox>(window, "StartupCheckBox")
            };

            // Act
            var visited = new List<object?>();
            for (var i = 0; i < expected.Length; i++)
            {
                AvaloniaTestHelper.PressKey(window, Key.Tab);
                visited.Add(Focused(window));
            }

            // Assert
            visited.Should().Equal(expected);
            Find<Grid>(window, "CustomTimePanel").IsEnabled.Should().BeFalse();
        });
    }

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
            var current = Find<RadioButton>(window, "CustomModeRadio");
            var expected = Find<RadioButton>(window, expectedRadio);
            current.Focus();
            AvaloniaTestHelper.FlushDispatcher();

            // Act
            var handled = AvaloniaTestHelper.PressKey(window, key);

            // Assert
            handled.Should().BeTrue();
            expected.IsChecked.Should().BeTrue();
            current.IsChecked.Should().BeFalse();
            Focused(window).Should().BeSameAs(expected);
            expected.IsTabStop.Should().BeTrue();
            current.IsTabStop.Should().BeFalse();
            Find<Grid>(window, "CustomTimePanel").IsEnabled.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(Key.Down, KeyModifiers.Control)]
    [InlineData(Key.Up, KeyModifiers.Shift)]
    [InlineData(Key.Right, KeyModifiers.Alt)]
    [InlineData(Key.Home, KeyModifiers.None)]
    public async Task ScheduleKey_ModifiedOrUnrelated_DoesNotChangeTheSelection(Key key, KeyModifiers modifiers)
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            var current = Find<RadioButton>(window, "CustomModeRadio");
            current.Focus();
            AvaloniaTestHelper.FlushDispatcher();

            // Act
            AvaloniaTestHelper.PressKey(window, key, modifiers);

            // Assert
            current.IsChecked.Should().BeTrue();
            Find<Grid>(window, "CustomTimePanel").IsEnabled.Should().BeTrue();
        });
    }

    [Theory]
    [InlineData('g', "StrengthSlider")]
    [InlineData('b', "BrightnessSlider")]
    [InlineData('o', "CustomOnInput")]
    [InlineData('f', "CustomOffInput")]
    [InlineData('t', "FadeDurationInput")]
    public async Task AccessKey_FieldLabel_FocusesTheEditor(char letter, string name)
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            Find<CheckBox>(window, "StartupCheckBox").Focus();
            AvaloniaTestHelper.FlushDispatcher();

            // Act
            AvaloniaTestHelper.PressAccessKey(window, letter);

            // Assert
            var target = Find<Control>(window, name);
            Focused(window).Should().Be(target is UserControl input ? Input(input) : target);
        });
    }

    [Theory]
    [InlineData('n', "ManualModeRadio")]
    [InlineData('u', "SunsetModeRadio")]
    [InlineData('h', "CustomModeRadio")]
    public async Task AccessKey_ScheduleOption_SelectsIt(char letter, string radioName)
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            Find<RadioButton>(window, letter == 'h' ? "ManualModeRadio" : "CustomModeRadio").IsChecked = true;

            // Act
            AvaloniaTestHelper.PressAccessKey(window, letter);

            // Assert
            Find<RadioButton>(window, radioName).IsChecked.Should().BeTrue();
            Find<Grid>(window, "CustomTimePanel").IsEnabled.Should().Be(letter == 'h');
        });
    }

    [Fact]
    public async Task AccessKey_Startup_TogglesTheCheckBox()
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            var startup = Find<CheckBox>(window, "StartupCheckBox");
            var before = startup.IsChecked;

            // Act
            AvaloniaTestHelper.PressAccessKey(window, 'a');

            // Assert
            startup.IsChecked.Should().Be(before != true);
        });
    }

    [Fact]
    public async Task AccessKey_Save_SavesAndClosesTheWindow()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var closed = WatchClosed(window);

            // Act
            AvaloniaTestHelper.PressAccessKey(window, 's');

            // Assert
            fixture.Runtime.Saved.Should().NotBeNull();
            closed().Should().BeTrue();
        });
    }

    [Fact]
    public async Task AccessKey_Cancel_ClosesWithoutSaving()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var closed = WatchClosed(window);

            // Act
            AvaloniaTestHelper.PressAccessKey(window, 'c');

            // Assert
            fixture.Runtime.Saved.Should().BeNull();
            closed().Should().BeTrue();
        });
    }

    [Fact]
    public async Task Enter_FromASlider_ActivatesSave()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var closed = WatchClosed(window);

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Enter);

            // Assert
            fixture.Runtime.Saved.Should().NotBeNull();
            closed().Should().BeTrue();
        });
    }

    [Fact]
    public async Task Enter_OnAFocusedCheckBox_TogglesItInsteadOfSaving()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var startup = Find<CheckBox>(window, "StartupCheckBox");
            var before = startup.IsChecked;
            startup.Focus();
            AvaloniaTestHelper.FlushDispatcher();

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Enter);

            // Assert
            startup.IsChecked.Should().Be(before != true);
            fixture.Runtime.Saved.Should().BeNull();
        });
    }

    [Fact]
    public async Task Escape_FromAControl_ActivatesCancel()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var closed = WatchClosed(window);

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Escape);

            // Assert
            fixture.Runtime.Saved.Should().BeNull();
            closed().Should().BeTrue();
        });
    }

    [Fact]
    public async Task Automation_FocusableInputs_ExposeNameHelpAndAccessKey()
    {
        await WithWindow((window, _) =>
        {
            // Arrange
            (string Name, string Label, string Shortcut)[] expected =
            [
                ("StrengthSlider", "Strength", "Alt+G"),
                ("BrightnessSlider", "Brightness", "Alt+B"),
                ("CustomOnInput", "Turn on", "Alt+O"),
                ("CustomOffInput", "Turn off", "Alt+F"),
                ("FadeDurationInput", "Transition duration (0 to 5000 ms)", "Alt+T")
            ];

            foreach (var (name, label, shortcut) in expected)
            {
                // Act
                var control = Find<Control>(window, name);
                var focusable = control is UserControl input ? Input(input) : control;
                var peer = ControlAutomationPeer.CreatePeerForElement(focusable);

                // Assert
                peer.GetName().Should().Be(label, name);
                peer.GetHelpText().Should().NotBeNullOrWhiteSpace(name);
                peer.GetAcceleratorKey().Should().Be(shortcut, name);
            }
        });
    }

    [Fact]
    public async Task Automation_Buttons_ExposeNamesWithoutAccessKeyMarkers()
    {
        await WithWindow((window, _) =>
        {
            // Act
            var names = new Control[]
                {
                    CancelButton(window),
                    Find<Button>(window, "SaveButton"),
                    Find<RadioButton>(window, "ManualModeRadio"),
                    Find<RadioButton>(window, "SunsetModeRadio"),
                    Find<RadioButton>(window, "CustomModeRadio"),
                    Find<CheckBox>(window, "StartupCheckBox")
                }
                .Select(static control => ControlAutomationPeer.CreatePeerForElement(control).GetName())
                .ToArray();

            // Assert
            names.Should().Equal(
                "Cancel",
                "Save",
                "Never (manual only)",
                "Sunset to sunrise",
                "Set hours",
                "Start automatically when I sign in");
        });
    }

    [Fact]
    public async Task Cancel_AfterSliderPreview_RestoresSavedGammaWithoutSaving()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var strength = Find<Slider>(window, "StrengthSlider");
            var brightness = Find<Slider>(window, "BrightnessSlider");

            // Act
            strength.Value = 40;
            brightness.Value = 80;
            var appliedBeforeDispatch = fixture.Gamma.Applied.Count;
            AvaloniaTestHelper.FlushDispatcher();
            var preview = fixture.Gamma.Applied[^1];
            CancelButton(window).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Assert
            appliedBeforeDispatch.Should().Be(0);
            preview.Should().Be((MainWindow.StrengthToTemperature(40), 80d));
            fixture.Gamma.Applied.Should().HaveCount(2);
            fixture.Gamma.Applied[^1].Should().Be((3400d, 75d));
            fixture.Runtime.Saved.Should().BeNull();
        });
    }

    [Fact]
    public async Task Close_WithPendingSliderPreview_DiscardsPreview()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var strength = Find<Slider>(window, "StrengthSlider");

            // Act
            strength.Value = 40;
            window.Close();
            AvaloniaTestHelper.FlushDispatcher();

            // Assert
            fixture.Gamma.Applied.Should().BeEmpty();
        });
    }

    [Fact]
    public async Task GammaError_WhileOpen_IsShownInlineUntilAnApplySucceeds()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var error = Find<TextBlock>(window, "GammaErrorText");
            fixture.Gamma.ApplyResults.Enqueue(false);

            // Act
            Find<Slider>(window, "StrengthSlider").Value = 40;
            AvaloniaTestHelper.FlushDispatcher();
            var shown = (error.IsVisible, error.Text);
            Find<Slider>(window, "StrengthSlider").Value = 41;
            AvaloniaTestHelper.FlushDispatcher();

            // Assert
            shown.IsVisible.Should().BeTrue();
            shown.Text.Should().NotBeNullOrWhiteSpace();
            error.IsVisible.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData("CustomOnInput", "1212:00 PM", "CustomOnErrorText")]
    [InlineData("CustomOffInput", "", "CustomOffErrorText")]
    [InlineData("FadeDurationInput", "5001", "FadeDurationErrorText")]
    [InlineData("FadeDurationInput", "", "FadeDurationErrorText")]
    [InlineData("FadeDurationInput", "99999999999999", "FadeDurationErrorText")]
    public async Task Save_InvalidInput_PreservesTextShowsInlineErrorFocusesItAndDoesNotSave(
        string inputName,
        string text,
        string errorName)
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var control = Find<UserControl>(window, inputName);
            var input = Input(control);
            input.Text = text;

            // Act
            Save(window);
            Save(window);

            // Assert
            fixture.Runtime.Saved.Should().BeNull();
            input.Text.Should().Be(text);
            HasError(control).Should().BeTrue();
            var error = Find<TextBlock>(window, errorName);
            error.Text.Should().Contain(ErrorMessage(control));
            error.IsEffectivelyVisible.Should().BeTrue();
            Focused(window).Should().BeSameAs(input);
        });
    }

    [Fact]
    public async Task Save_MultipleInvalidInputs_ReportsEveryFieldFocusesTheFirstThenSavesCorrections()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var on = Find<TimeUpDown>(window, "CustomOnInput");
            var off = Find<TimeUpDown>(window, "CustomOffInput");
            var duration = Find<NumericUpDown>(window, "FadeDurationInput");
            Input(on).Text = "invalid";
            Input(off).Text = "invalid";
            Input(duration).Text = "5001";

            // Act
            Save(window);
            var errors = new[] { HasError(on), HasError(off), HasError(duration) };
            var focused = Focused(window);
            Input(on).Text = "22:30";
            Input(off).Text = "06:15";
            Input(duration).Text = "5000";
            Save(window);

            // Assert
            errors.Should().AllBeEquivalentTo(true);
            focused.Should().BeSameAs(Input(on));
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
            var on = Find<TimeUpDown>(window, "CustomOnInput");
            Input(on).Text = "invalid";
            on.CommitEdit();
            Find<RadioButton>(window, radioName).IsChecked = true;
            var errorsShown = Find<StackPanel>(window, "ScheduleErrors").IsEffectivelyVisible;

            // Act
            Save(window);

            // Assert
            errorsShown.Should().BeFalse();
            fixture.Runtime.Saved.Should().NotBeNull();
            fixture.Runtime.Saved.Mode.Should().Be((ScheduleMode)mode);
            fixture.Runtime.Saved.CustomOn.Should().Be("21:00");
        });
    }

    [Fact]
    public async Task Save_MatchingScheduleTimes_ShowsInlineErrorThatClearsWhenCorrected()
    {
        await WithWindow((window, fixture) =>
        {
            // Arrange
            var input = Input(Find<TimeUpDown>(window, "CustomOffInput"));
            input.Text = "21:00";

            // Act
            Save(window);
            var error = Find<TextBlock>(window, "ScheduleErrorText");
            var message = error.Text;
            var savedBeforeCorrection = fixture.Runtime.Saved;
            var focused = Focused(window);
            input.Text = "07:00";
            var correctedMessage = error.Text;
            Save(window);

            // Assert
            savedBeforeCorrection.Should().BeNull();
            focused.Should().BeSameAs(input);
            message.Should().Contain("different");
            correctedMessage.Should().BeEmpty();
            fixture.Runtime.Saved.Should().NotBeNull();
            fixture.Runtime.Saved.CustomOff.Should().Be("07:00");
        });
    }

    [Theory]
    [InlineData(3400, 67, 3400)]
    [InlineData(3400, 67.4, 3400)]
    [InlineData(3400, 66.5, 3464)]
    [InlineData(3400, 67.6, 3372)]
    [InlineData(6500, -1, 6500)]
    [InlineData(1900, 101, 1900)]
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
        var settings = MainWindow.BuildSettings(3400, 67, brightness, (ScheduleMode)mode, customOn, customOff, 1234);

        // Assert
        settings.Brightness.Should().Be(expectedBrightness);
        settings.Mode.Should().Be((ScheduleMode)mode);
        settings.CustomOn.Should().Be("21:35");
        settings.CustomOff.Should().Be("07:05");
        settings.FadeMs.Should().Be(1234);
    }

    [Theory]
    [InlineData(6500, 0)]
    [InlineData(3400, 67)]
    [InlineData(1900, 100)]
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
    [InlineData(1, 6454)]
    [InlineData(50, 4200)]
    [InlineData(99, 1946)]
    [InlineData(100, 1900)]
    [InlineData(101, 1900)]
    public void StrengthToTemperature_Strength_ClampsAndConvertsTemperature(int strength, int expected)
    {
        // Act
        var result = MainWindow.StrengthToTemperature(strength);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(21, 7, true)]
    [InlineData(0, 1, true)]
    [InlineData(7, 7, false)]
    public void IsCustomWindowValid_ScheduleTimes_DetectsZeroLengthWindow(int onHours, int offHours, bool expected)
    {
        // Act
        var result = MainWindow.IsCustomWindowValid(TimeSpan.FromHours(onHours), TimeSpan.FromHours(offHours));

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
        var result = MainWindow.IsCustomWindowValid(on, off);

        // Assert
        result.Should().BeFalse();
    }

    private static Task WithWindow(Action<MainWindow, AppControllerTests.ControllerFixture> action)
    {
        return AvaloniaTestHelper.RunAsync(() =>
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
            using var fixture = new AppControllerTests.ControllerFixture();
            var window = new MainWindow(fixture.Controller);
            var closed = WatchClosed(window);
            AvaloniaTestHelper.Show(window);
            try
            {
                action(window, fixture);
            }
            finally
            {
                if (!closed())
                {
                    window.Close();
                }
            }
        });
    }

    private static Func<bool> WatchClosed(Window window)
    {
        var closed = false;
        window.Closed += (_, _) => closed = true;
        return () => closed;
    }

    private static T Find<T>(MainWindow window, string name)
        where T : Control
    {
        return window.FindControl<T>(name)!;
    }

    private static IInputElement? Focused(TopLevel window)
    {
        return window.FocusManager.GetFocusedElement();
    }

    private static Button CancelButton(MainWindow window)
    {
        return window.GetVisualDescendants().OfType<Button>().Single(static button => button.IsCancel);
    }

    private static void Save(MainWindow window)
    {
        Find<Button>(window, "SaveButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        AvaloniaTestHelper.FlushDispatcher();
    }
}
