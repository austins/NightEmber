using NightEmber.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

namespace NightEmber.Tests.Unit.Controls;

public sealed class TimeUpDownTests
{
    [Theory]
    [InlineData(-1, 1439)]
    [InlineData(0, 0)]
    [InlineData(1440, 0)]
    [InlineData(1505, 65)]
    [InlineData(-1441, 1439)]
    public async Task TimeValue_OutsideDay_WrapsIntoTimeOfDay(int minutes, int expectedMinutes)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new TimeUpDown();

            // Act
            control.TimeValue = TimeSpan.FromMinutes(minutes);

            // Assert
            control.TimeValue.Should().Be(TimeSpan.FromMinutes(expectedMinutes));
        });
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(5, 5)]
    [InlineData(60, 59)]
    public async Task MinuteIncrement_OutOfRange_IsClamped(int increment, int expected)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new TimeUpDown();

            // Act
            control.MinuteIncrement = increment;

            // Assert
            control.MinuteIncrement.Should().Be(expected);
        });
    }

    [Theory]
    [InlineData("en-US", "h:mm tt", "9:45 PM", 21, 45)]
    [InlineData("en-GB", "HH:mm", "21:45", 21, 45)]
    [InlineData("de-DE", "HH:mm", "07:05", 7, 5)]
    [InlineData("en-US", "h:mm tt", "12:00 AM", 0, 0)]
    [InlineData("en-US", "h:mm tt", "12:00 PM", 12, 0)]
    [InlineData("en-US", "h:mm t", "7:05 P", 19, 5)]
    [InlineData("en-US", "tt h:mm", "PM 7:05", 19, 5)]
    public async Task CommitEdit_CultureSpecificTime_UsesCurrentCulture(
        string cultureName,
        string pattern,
        string text,
        int hour,
        int minute)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture(cultureName, pattern);
            var control = new TimeUpDown();
            Input(control).Text = text;

            // Act
            var committed = control.CommitEdit();

            // Assert
            committed.Should().BeTrue();
            control.TimeValue.Should().Be(new TimeSpan(hour, minute, 0));
            Input(control).Text.Should().Be(text);
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a time")]
    [InlineData("25:99")]
    [InlineData("1212:00 PM")]
    [InlineData("01/01/2026")]
    [InlineData("01/01/0001")]
    [InlineData("01/01/0001 09:00")]
    public async Task CommitEdit_InvalidText_PreservesEditAndPreviousValue(string text)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            Input(control).Text = text;

            // Act
            var committed = control.CommitEdit();

            // Assert
            committed.Should().BeFalse();
            control.TimeValue.Should().Be(new TimeSpan(7, 5, 0));
            Input(control).Text.Should().Be(text);
            InputValidation.GetHasError(control).Should().BeTrue();
            InputValidation.GetErrorMessage(control).Should().Contain("21:00");
        });
    }

    [Theory]
    [InlineData(Key.Up, 23, 0)]
    [InlineData(Key.Down, 0, 23)]
    public async Task HourAdjustment_DayBoundary_WrapsAtMidnight(Key key, int initialHour, int expectedHour)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(initialHour, 15, 0) };

            // Act
            var args = WpfTestHelper.PressKey(Input(control), key);

            // Assert
            args.Handled.Should().BeTrue();
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, 15, 0));
            Input(control).SelectedText.Should().Be(expectedHour.ToString("D2", CultureInfo.InvariantCulture));
        });
    }

    [Theory]
    [InlineData(Key.Up, 23, 58, 0, 3)]
    [InlineData(Key.Down, 0, 2, 23, 57)]
    public async Task MinuteAdjustment_DayBoundary_UsesIncrementAndWraps(
        Key key,
        int hour,
        int minute,
        int expectedHour,
        int expectedMinute)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown
            {
                TimeValue = new TimeSpan(hour, minute, 0),
                MinuteIncrement = 5
            };
            WpfTestHelper.PressKey(Input(control), Key.Right);

            // Act
            WpfTestHelper.PressKey(Input(control), key);

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, expectedMinute, 0));
            Input(control).SelectedText.Should().Be(expectedMinute.ToString("D2", CultureInfo.InvariantCulture));
        });
    }

    [Theory]
    [InlineData("h:mm tt", 7, "PM")]
    [InlineData("tt h:mm", 0, "PM")]
    [InlineData("h:mm t", 5, "P")]
    public async Task PeriodAdjustment_CustomPatterns_SupportsPrefixSuffixAndAbbreviation(
        string pattern,
        int caretIndex,
        string expectedSelection)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-US", pattern);
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var input = Input(control);
            input.CaretIndex = caretIndex;
            input.RaiseEvent(
                new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent
                });

            // Act
            WpfTestHelper.PressKey(input, Key.Up);

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(19, 5, 0));
            input.SelectedText.Should().Be(expectedSelection);
        });
    }

    [Fact]
    public async Task SegmentNavigation_FormattedTime_SelectsAndWrapsSegments()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-US", "h:mm tt");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var input = Input(control);

            // Act
            WpfTestHelper.PressKey(input, Key.Right);
            var minute = input.SelectedText;
            WpfTestHelper.PressKey(input, Key.Right);
            var period = input.SelectedText;
            WpfTestHelper.PressKey(input, Key.Right);
            var wrappedHour = input.SelectedText;
            WpfTestHelper.PressKey(input, Key.Left);

            // Assert
            minute.Should().Be("05");
            period.Should().Be("AM");
            wrappedHour.Should().Be("7");
            input.SelectedText.Should().Be("AM");
        });
    }

    [Theory]
    [InlineData(0, 8)]
    [InlineData(1, 6)]
    public async Task SpinnerButtons_HourSelected_AdjustsHour(int buttonIndex, int expectedHour)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var layout = (Grid)((Border)control.Content).Child;
            var buttons = (Grid)layout.Children[1];

            // Act
            ((RepeatButton)buttons.Children[buttonIndex]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, 5, 0));
        });
    }

    [Theory]
    [InlineData(Key.Enter, 21)]
    [InlineData(Key.Escape, 7)]
    public async Task EditKeys_PendingText_CommitsOrCancels(Key key, int expectedHour)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            Input(control).Text = "21:05";

            // Act
            var args = WpfTestHelper.PressKey(Input(control), key);

            // Assert
            args.Handled.Should().BeTrue();
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, 5, 0));
            Input(control).Text.Should().Be($"{expectedHour:D2}:05");
        });
    }

    [Fact]
    public async Task CommitEdit_TwoWayBoundValue_PreservesBindingAndUpdatesSource()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var source = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var control = new TimeUpDown();
            BindingOperations.SetBinding(
                control,
                TimeUpDown.TimeValueProperty,
                new Binding(nameof(TimeUpDown.TimeValue)) { Source = source });
            Input(control).Text = "21:45";

            // Act
            var committed = control.CommitEdit();

            // Assert
            committed.Should().BeTrue();
            BindingOperations.IsDataBound(control, TimeUpDown.TimeValueProperty).Should().BeTrue();
            source.TimeValue.Should().Be(new TimeSpan(21, 45, 0));
        });
    }

    [Fact]
    public async Task LostKeyboardFocus_PendingEdit_CommitsValue()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown();
            var input = Input(control);
            input.Text = "21:45";

            // Act
            input.RaiseEvent(
                new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, null)
                {
                    RoutedEvent = Keyboard.LostKeyboardFocusEvent
                });

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(21, 45, 0));
        });
    }

    [Fact]
    public async Task MouseWheel_WithoutKeyboardFocus_DoesNotChangeValueOrConsumeScroll()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120)
            {
                RoutedEvent = Mouse.PreviewMouseWheelEvent
            };

            // Act
            control.RaiseEvent(args);

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(7, 5, 0));
            args.Handled.Should().BeFalse();
        });
    }

    private static TextBox Input(TimeUpDown control)
    {
        return (TextBox)control.FindName("ValueTextBox");
    }

    private static void SetCulture(string name, string shortTimePattern)
    {
        var culture = new CultureInfo(name);
        culture.DateTimeFormat.ShortTimePattern = shortTimePattern;
        CultureInfo.CurrentCulture = culture;
    }
}
