using NightEmber.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

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
        await WpfTestHelper.RunAsync(() =>
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
        await WpfTestHelper.RunAsync(() =>
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
        await WpfTestHelper.RunAsync(() =>
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
    [InlineData("35", 35)]
    [InlineData("150", 100)]
    [InlineData("", 25)]
    [InlineData("invalid", 25)]
    [InlineData("12.5", 25)]
    [InlineData("-10", 25)]
    [InlineData("2147483648", 25)]
    public async Task CommitEdit_ValidOrInvalidText_ClampsOrRestoresValue(string text, int expected)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var control = new NumericUpDown { NumericValue = 25 };
            Input(control).Text = text;

            // Act
            control.CommitEdit();

            // Assert
            control.NumericValue.Should().Be(expected);
            Input(control).Text.Should().Be(expected.ToString(CultureInfo.InvariantCulture));
        });
    }

    [Theory]
    [InlineData(Key.Up, 95, 100)]
    [InlineData(Key.Down, 5, 0)]
    [InlineData(Key.Up, 40, 50)]
    [InlineData(Key.Down, 40, 30)]
    public async Task ArrowKeys_ValueNearBoundary_AdjustsAndClamps(Key key, int initial, int expected)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown
            {
                NumericValue = initial,
                Increment = 10
            };

            // Act
            var args = WpfTestHelper.PressKey(Input(control), key);

            // Assert
            args.Handled.Should().BeTrue();
            control.NumericValue.Should().Be(expected);
            Input(control).SelectedText.Should().Be(Input(control).Text);
        });
    }

    [Theory]
    [InlineData(Key.Enter, 42)]
    [InlineData(Key.Escape, 25)]
    public async Task EditKeys_PendingText_CommitsOrCancels(Key key, int expected)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 25 };
            Input(control).Text = "42";

            // Act
            var args = WpfTestHelper.PressKey(Input(control), key);

            // Assert
            args.Handled.Should().BeTrue();
            control.NumericValue.Should().Be(expected);
            Input(control).Text.Should().Be(expected.ToString(CultureInfo.CurrentCulture));
        });
    }

    [Theory]
    [InlineData(0, 45)]
    [InlineData(1, 35)]
    public async Task SpinnerButtons_PendingEdit_CommitsBeforeAdjusting(int buttonIndex, int expected)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown
            {
                NumericValue = 25,
                Increment = 5
            };
            Input(control).Text = "40";
            var layout = (Grid)((Border)control.Content).Child;
            var buttons = (Grid)layout.Children[1];

            // Act
            ((RepeatButton)buttons.Children[buttonIndex]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

            // Assert
            control.NumericValue.Should().Be(expected);
        });
    }

    [Fact]
    public async Task CommitEdit_TwoWayBoundValue_PreservesBindingAndUpdatesSource()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var source = new NumericUpDown { NumericValue = 25 };
            var control = new NumericUpDown();
            BindingOperations.SetBinding(
                control,
                NumericUpDown.NumericValueProperty,
                new Binding(nameof(NumericUpDown.NumericValue)) { Source = source });
            Input(control).Text = "42";

            // Act
            control.CommitEdit();

            // Assert
            BindingOperations.IsDataBound(control, NumericUpDown.NumericValueProperty).Should().BeTrue();
            source.NumericValue.Should().Be(42);
        });
    }

    [Fact]
    public async Task LostKeyboardFocus_PendingEdit_CommitsValue()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 25 };
            var input = Input(control);
            input.Text = "42";

            // Act
            input.RaiseEvent(
                new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, null)
                {
                    RoutedEvent = Keyboard.LostKeyboardFocusEvent
                });

            // Assert
            control.NumericValue.Should().Be(42);
        });
    }

    [Theory]
    [InlineData("42", false)]
    [InlineData("", true)]
    [InlineData("-42", true)]
    [InlineData("4 2", true)]
    [InlineData("12.5", true)]
    [InlineData("abc", true)]
    public async Task Paste_Text_RejectsAnythingExceptDigits(string text, bool cancelled)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown();
            var data = new DataObject(DataFormats.UnicodeText, text);
            var args = new DataObjectPastingEventArgs(data, false, DataFormats.UnicodeText);

            // Act
            Input(control).RaiseEvent(args);

            // Assert
            args.CommandCancelled.Should().Be(cancelled);
        });
    }

    [Fact]
    public async Task Paste_NonTextData_IsCancelled()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown();
            var data = new DataObject("CustomBinary", new byte[] { 1 });
            var args = new DataObjectPastingEventArgs(data, false, "CustomBinary");

            // Act
            Input(control).RaiseEvent(args);

            // Assert
            args.CommandCancelled.Should().BeTrue();
        });
    }

    [Theory]
    [InlineData("42", false)]
    [InlineData("a", true)]
    [InlineData("-", true)]
    public async Task TextInput_Text_RejectsNonDigits(string text, bool handled)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown();
            var input = Input(control);
            var composition = new TextComposition(InputManager.Current, input, text);
            var args = new TextCompositionEventArgs(Keyboard.PrimaryDevice, composition)
            {
                RoutedEvent = TextCompositionManager.PreviewTextInputEvent
            };

            // Act
            input.RaiseEvent(args);

            // Assert
            args.Handled.Should().Be(handled);
        });
    }

    [Fact]
    public async Task MouseWheel_WithoutKeyboardFocus_DoesNotChangeValueOrConsumeScroll()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = new NumericUpDown { NumericValue = 25 };
            var args = new MouseWheelEventArgs(Mouse.PrimaryDevice, 0, 120)
            {
                RoutedEvent = Mouse.PreviewMouseWheelEvent
            };

            // Act
            control.RaiseEvent(args);

            // Assert
            control.NumericValue.Should().Be(25);
            args.Handled.Should().BeFalse();
        });
    }

    private static TextBox Input(NumericUpDown control)
    {
        return (TextBox)control.FindName("ValueTextBox");
    }
}
