using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Globalization;
using static NightEmber.Tests.Unit.Controls.SpinnerTestHelper;
using NumericUpDown = NightEmber.Controls.NumericUpDown;

namespace NightEmber.Tests.Unit.Controls;

public sealed class NumericUpDownTests
{
    [Theory]
    [InlineData(-1, 10)]
    [InlineData(10, 10)]
    [InlineData(50, 50)]
    [InlineData(101, 100)]
    public async Task NumericValue_OutsideRange_IsCoerced(int value, int expected)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown
            {
                Minimum = 10,
                Maximum = 100
            };

            // Act
            control.NumericValue = value;

            // Assert
            control.NumericValue.Should().Be(expected);
            Input(control).Text.Should().Be(expected.ToString(CultureInfo.CurrentCulture));
        });
    }

    [Fact]
    public async Task RangeChanges_ExistingValue_RecoercesAndRefreshesText()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 50 };

            // Act
            control.Maximum = 40;
            var afterMaximum = (control.NumericValue, Input(control).Text);
            control.Maximum = 100;
            control.Minimum = 60;

            // Assert
            afterMaximum.Should().Be((40, "40"));
            control.NumericValue.Should().Be(60);
            Input(control).Text.Should().Be("60");
        });
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(25, 25)]
    public async Task Increment_InvalidValue_IsCoercedToPositive(int increment, int expected)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown();

            // Act
            control.Increment = increment;

            // Assert
            control.Increment.Should().Be(expected);
        });
    }

    [Theory]
    [InlineData("0", 0, true)]
    [InlineData("35", 35, true)]
    [InlineData("100", 100, true)]
    [InlineData("150", 25, false)]
    [InlineData("", 25, false)]
    [InlineData("invalid", 25, false)]
    [InlineData("12.5", 25, false)]
    [InlineData("-10", 25, false)]
    [InlineData("2147483648", 25, false)]
    public async Task CommitEdit_ValidOrInvalidText_CommitsOrPreservesInvalidEdit(string text, int expected, bool valid)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var control = new NumericUpDown { NumericValue = 25 };
            Input(control).Text = text;

            // Act
            var committed = control.CommitEdit();

            // Assert
            committed.Should().Be(valid);
            control.NumericValue.Should().Be(expected);
            Input(control).Text.Should().Be(text);
            HasError(control).Should().Be(!valid);
        });
    }

    [Theory]
    [InlineData(Key.Up, 95, 100)]
    [InlineData(Key.Down, 5, 0)]
    [InlineData(Key.Up, 40, 50)]
    [InlineData(Key.Down, 40, 30)]
    public async Task ArrowKeys_ValueNearBoundary_AdjustsAndClamps(Key key, int initial, int expected)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown
            {
                NumericValue = initial,
                Increment = 10
            };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);

            // Act
            var handled = AvaloniaTestHelper.PressKey(window, key);

            // Assert
            handled.Should().BeTrue();
            control.NumericValue.Should().Be(expected);
            Input(control).SelectedText.Should().Be(Input(control).Text);
            window.Close();
        });
    }

    [Theory]
    [InlineData(Key.Enter, 42)]
    [InlineData(Key.Escape, 25)]
    public async Task EditKeys_PendingText_CommitsOrCancels(Key key, int expected)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 25 };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            Input(control).Text = "42";

            // Act
            var handled = AvaloniaTestHelper.PressKey(window, key);

            // Assert
            handled.Should().BeTrue();
            control.NumericValue.Should().Be(expected);
            Input(control).Text.Should().Be(expected.ToString(CultureInfo.CurrentCulture));
            window.Close();
        });
    }

    [Theory]
    [InlineData(true, 45)]
    [InlineData(false, 35)]
    public async Task SpinnerButtons_PendingEdit_CommitsBeforeAdjusting(bool increase, int expected)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown
            {
                NumericValue = 25,
                Increment = 5
            };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Input(control).Text = "40";

            // Act
            ArrowButton(control, increase).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            // Assert
            control.NumericValue.Should().Be(expected);
            window.Close();
        });
    }

    [Fact]
    public async Task CommitEdit_TwoWayBoundValue_PreservesBindingAndUpdatesSource()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var source = new NumericUpDown { NumericValue = 25 };
            var control = new NumericUpDown();
            control.Bind(
                NumericUpDown.NumericValueProperty,
                new Binding(nameof(NumericUpDown.NumericValue))
                {
                    Source = source,
                    Mode = BindingMode.TwoWay
                });
            Input(control).Text = "42";

            // Act
            control.CommitEdit();
            var sourceAfterCommit = source.NumericValue;
            source.NumericValue = 60;

            // Assert
            sourceAfterCommit.Should().Be(42);
            control.NumericValue.Should().Be(60);
            Input(control).Text.Should().Be("60");
        });
    }

    [Fact]
    public async Task LostFocus_PendingEdit_CommitsValue()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 25 };
            var window = ShowWithNeighbor(control, out var other);
            Focus(control);
            Input(control).Text = "42";

            // Act
            other.Focus();

            // Assert
            control.NumericValue.Should().Be(42);
            window.Close();
        });
    }

    [Theory]
    [InlineData("42")]
    [InlineData("-42")]
    [InlineData("4 2")]
    [InlineData("12.5")]
    [InlineData("abc")]
    [InlineData("-")]
    [InlineData(".")]
    public async Task Typing_Text_IsNotFilteredAndLeavesValidationToCommit(string text)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 25 };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            Input(control).SelectAll();

            // Act
            window.KeyTextInput(text);

            // Assert
            Input(control).Text.Should().Be(text);
            HasError(control).Should().BeFalse();
            control.NumericValue.Should().Be(25);
            window.Close();
        });
    }

    [Fact]
    public async Task MouseWheel_WithoutKeyboardFocus_DoesNotChangeValueOrConsumeScroll()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 25 };
            var window = ShowWithNeighbor(control, out var other);
            other.Focus();

            // Act
            var handled = WheelOver(window, control);

            // Assert
            control.NumericValue.Should().Be(25);
            handled.Should().BeFalse();
            window.Close();
        });
    }

    [Fact]
    public async Task MouseWheel_WithKeyboardFocus_AdjustsByTheIncrement()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown
            {
                NumericValue = 25,
                Increment = 5
            };
            var window = ShowWithNeighbor(control, out _);
            Focus(control);

            // Act
            WheelOver(window, control);

            // Assert
            control.NumericValue.Should().Be(30);
            window.Close();
        });
    }
}
