using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using NightEmber.Controls;
using System.Globalization;
using static NightEmber.Tests.Unit.Controls.SpinnerTestHelper;

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
        await AvaloniaTestHelper.RunAsync(() =>
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
        await AvaloniaTestHelper.RunAsync(() =>
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
        await AvaloniaTestHelper.RunAsync(() =>
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
        await AvaloniaTestHelper.RunAsync(() =>
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
            HasError(control).Should().BeTrue();
            control.ErrorMessage.Should().Contain("21:00");
        });
    }

    [Theory]
    [InlineData(Key.Up, 23, 0)]
    [InlineData(Key.Down, 0, 23)]
    public async Task HourAdjustment_DayBoundary_WrapsAtMidnight(Key key, int initialHour, int expectedHour)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(initialHour, 15, 0) };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);

            // Act
            var handled = AvaloniaTestHelper.PressKey(window, key);

            // Assert
            handled.Should().BeTrue();
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, 15, 0));
            Input(control).SelectedText.Should().Be(expectedHour.ToString("D2", CultureInfo.InvariantCulture));
            window.Close();
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
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown
            {
                TimeValue = new TimeSpan(hour, minute, 0),
                MinuteIncrement = 5
            };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            AvaloniaTestHelper.PressKey(window, Key.Right);

            // Act
            AvaloniaTestHelper.PressKey(window, key);

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, expectedMinute, 0));
            Input(control).SelectedText.Should().Be(expectedMinute.ToString("D2", CultureInfo.InvariantCulture));
            window.Close();
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
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-US", pattern);
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);
            input.CaretIndex = caretIndex;
            window.MouseUp(Center(input, window), MouseButton.Left);

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Up);

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(19, 5, 0));
            input.SelectedText.Should().Be(expectedSelection);
            window.Close();
        });
    }

    [Fact]
    public async Task SegmentNavigation_FormattedTime_SelectsAndWrapsSegments()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-US", "h:mm tt");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);

            // Act
            var initial = input.SelectedText;
            AvaloniaTestHelper.PressKey(window, Key.Right);
            var minute = input.SelectedText;
            AvaloniaTestHelper.PressKey(window, Key.Right);
            var period = input.SelectedText;
            AvaloniaTestHelper.PressKey(window, Key.Right);
            var wrappedHour = input.SelectedText;
            AvaloniaTestHelper.PressKey(window, Key.Left);

            // Assert
            initial.Should().Be("7");
            minute.Should().Be("05");
            period.Should().Be("AM");
            wrappedHour.Should().Be("7");
            input.SelectedText.Should().Be("AM");
            window.Close();
        });
    }

    [Theory]
    [InlineData(true, 8)]
    [InlineData(false, 6)]
    public async Task SpinnerButtons_HourSelected_AdjustsHour(bool increase, int expectedHour)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var window = AvaloniaTestHelper.ShowInWindow(control);

            // Act
            ArrowButton(control, increase).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, 5, 0));
            window.Close();
        });
    }

    [Theory]
    [InlineData(Key.Enter, 21)]
    [InlineData(Key.Escape, 7)]
    public async Task EditKeys_PendingText_CommitsOrCancels(Key key, int expectedHour)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            Input(control).Text = "21:05";

            // Act
            var handled = AvaloniaTestHelper.PressKey(window, key);

            // Assert
            handled.Should().BeTrue();
            control.TimeValue.Should().Be(new TimeSpan(expectedHour, 5, 0));
            Input(control).Text.Should().Be($"{expectedHour:D2}:05");
            window.Close();
        });
    }

    [Fact]
    public async Task TypingMode_TypedText_KeepsCaretNavigationUntilCommitted()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);
            input.SelectAll();
            window.KeyTextInput("21:30");

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Left);
            var caretAfterLeft = input.CaretIndex;
            var textAfterLeft = input.Text;
            AvaloniaTestHelper.PressKey(window, Key.Enter);
            var selectionAfterCommit = input.SelectedText;
            AvaloniaTestHelper.PressKey(window, Key.Right);

            // Assert
            caretAfterLeft.Should().Be(4);
            textAfterLeft.Should().Be("21:30");
            control.TimeValue.Should().Be(new TimeSpan(21, 30, 0));
            selectionAfterCommit.Should().Be("21");
            input.SelectedText.Should().Be("30");
            window.Close();
        });
    }

    [Fact]
    public async Task CommitEdit_TwoWayBoundValue_PreservesBindingAndUpdatesSource()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var source = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var control = new TimeUpDown();
            control.Bind(
                TimeUpDown.TimeValueProperty,
                new Binding(nameof(TimeUpDown.TimeValue))
                {
                    Source = source,
                    Mode = BindingMode.TwoWay
                });
            Input(control).Text = "21:45";

            // Act
            var committed = control.CommitEdit();
            var sourceAfterCommit = source.TimeValue;
            source.TimeValue = new TimeSpan(6, 0, 0);

            // Assert
            committed.Should().BeTrue();
            sourceAfterCommit.Should().Be(new TimeSpan(21, 45, 0));
            control.TimeValue.Should().Be(new TimeSpan(6, 0, 0));
            Input(control).Text.Should().Be("06:00");
        });
    }

    [Fact]
    public async Task LostFocus_PendingEdit_CommitsValue()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            SetCulture("en-GB", "HH:mm");
            var control = new TimeUpDown();
            var window = ShowWithNeighbor(control, out var other);
            Focus(control);
            Input(control).Text = "21:45";

            // Act
            other.Focus();

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(21, 45, 0));
            window.Close();
        });
    }

    [Fact]
    public async Task MouseWheel_WithoutKeyboardFocus_DoesNotChangeValueOrConsumeScroll()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new TimeUpDown { TimeValue = new TimeSpan(7, 5, 0) };
            var window = ShowWithNeighbor(control, out var other);
            other.Focus();

            // Act
            var handled = WheelOver(window, control);

            // Assert
            control.TimeValue.Should().Be(new TimeSpan(7, 5, 0));
            handled.Should().BeFalse();
            window.Close();
        });
    }

    private static void SetCulture(string name, string shortTimePattern)
    {
        var culture = new CultureInfo(name);
        culture.DateTimeFormat.ShortTimePattern = shortTimePattern;
        CultureInfo.CurrentCulture = culture;
    }
}
