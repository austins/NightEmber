using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using NightEmber.Controls;
using System.Globalization;
using static NightEmber.Tests.Unit.Controls.SpinnerTestHelper;
using NumericUpDown = NightEmber.Controls.NumericUpDown;

namespace NightEmber.Tests.Unit.Controls;

public sealed class SpinnerValidationTests
{
    [Theory]
    [InlineData(false, Key.Up, KeyModifiers.Control)]
    [InlineData(false, Key.Down, KeyModifiers.Shift)]
    [InlineData(false, Key.Enter, KeyModifiers.Alt)]
    [InlineData(true, Key.Up, KeyModifiers.Control)]
    [InlineData(true, Key.Down, KeyModifiers.Shift)]
    [InlineData(true, Key.Enter, KeyModifiers.Alt)]
    public async Task ModifiedKey_InvalidEdit_KeepsTextEditingBehaviorWithoutValidating(
        bool timeInput,
        Key key,
        KeyModifiers modifiers)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);
            input.Text = "invalid";

            // Act
            AvaloniaTestHelper.PressKey(window, key, modifiers);

            // Assert
            input.Text.Should().Be("invalid");
            HasError(control).Should().BeFalse();
            PreviousValueIsUnchanged(control);
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostFocus_InvalidEdit_PreservesTextAndExposesAccessibleError(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var window = ShowWithNeighbor(control, out var other);
            Focus(control);
            var input = Input(control);
            input.Text = "invalid";
            var errorBeforeCommit = HasError(control);

            // Act
            other.Focus();

            // Assert
            errorBeforeCommit.Should().BeFalse();
            input.Text.Should().Be("invalid");
            other.IsFocused.Should().BeTrue();
            input.IsFocused.Should().BeFalse();
            HasError(control).Should().BeTrue();
            AutomationProperties.GetItemStatus(input).Should().Be(ErrorMessage(control));
            AutomationProperties.GetItemStatus(input).Should().NotBeNullOrWhiteSpace();
            PreviousValueIsUnchanged(control);
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Correction_AfterValidation_UpdatesErrorWithoutDiscardingText(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var input = Input(control);
            input.Text = "invalid";
            Commit(control);

            // Act
            input.Text = timeInput ? "09:45" : "5000";
            var corrected = HasError(control);
            input.Text = timeInput ? "99:45" : "5001";
            var invalidAgain = HasError(control);
            input.Text = timeInput ? "09:45" : "5000";
            var committed = Commit(control);

            // Assert
            corrected.Should().BeFalse();
            invalidAgain.Should().BeTrue();
            committed.Should().BeTrue();
            HasError(control).Should().BeFalse();
            ErrorMessage(control).Should().BeEmpty();
            input.Text.Should().Be(timeInput ? "09:45" : "5000");
        });
    }

    [Theory]
    [InlineData(false, Key.Enter)]
    [InlineData(false, Key.Up)]
    [InlineData(false, Key.Down)]
    [InlineData(true, Key.Enter)]
    [InlineData(true, Key.Up)]
    [InlineData(true, Key.Down)]
    public async Task AdjustmentOrCommit_InvalidEdit_DoesNotSilentlyReplaceIt(bool timeInput, Key key)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);
            input.Text = "invalid";

            // Act
            var handled = AvaloniaTestHelper.PressKey(window, key);

            // Assert
            handled.Should().BeTrue();
            input.Text.Should().Be("invalid");
            HasError(control).Should().BeTrue();
            PreviousValueIsUnchanged(control);
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Escape_InvalidEdit_RestoresValueAndClearsError(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);
            input.Text = "invalid";
            Commit(control);

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Escape);

            // Assert
            input.Text.Should().Be(timeInput ? "21:00" : "300");
            HasError(control).Should().BeFalse();
            AutomationProperties.GetItemStatus(input).Should().Be(timeInput ? "Hour selected" : null);
            PreviousValueIsUnchanged(control);
            window.Close();
        });
    }

    [Theory]
    [InlineData(Key.Left, 4)]
    [InlineData(Key.Right, 6)]
    public async Task HorizontalArrows_PendingTimeEdit_AllowNormalCaretNavigation(Key key, int expectedCaret)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(true);
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);
            input.Text = "1212:00 PM";
            input.CaretIndex = 5;

            // Act
            AvaloniaTestHelper.PressKey(window, key);

            // Assert
            input.Text.Should().Be("1212:00 PM");
            input.CaretIndex.Should().Be(expectedCaret);
            input.SelectedText.Should().BeEmpty();
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidEdit_Outline_ShowsOneErrorMessageOwnedByTheWindow(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Input(control).Text = "invalid";
            var heightBefore = control.Bounds.Height;

            // Act
            Commit(control);
            window.UpdateLayout();

            // Assert
            HasError(control).Should().BeTrue();
            control.Bounds.Height.Should().Be(heightBefore);
            Spinner(control)
                .GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(static text => text.IsVisible && !string.IsNullOrEmpty(text.Text))
                .Should()
                .BeEmpty("the settings window shows the message, so the control only draws the outline");
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArrowClick_InvalidEdit_KeepsEditorFocusSoEscapeRevertsInsteadOfCancelling(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var cancelled = false;
            var cancel = new Button { Content = "Cancel", IsCancel = true };
            cancel.Click += (_, _) => cancelled = true;
            var panel = new StackPanel();
            panel.Children.Add(control);
            panel.Children.Add(cancel);
            var window = AvaloniaTestHelper.ShowInWindow(panel);
            Focus(control);
            var input = Input(control);
            input.Text = "invalid";
            var arrow = ArrowButton(control, true);

            // Act
            window.MouseDown(Center(arrow, window), MouseButton.Left);
            window.MouseUp(Center(arrow, window), MouseButton.Left);
            AvaloniaTestHelper.PressKey(window, Key.Escape);

            // Assert
            input.Text.Should().Be(timeInput ? "21:00" : "300");
            HasError(control).Should().BeFalse();
            cancelled.Should().BeFalse();
            window.Close();
        });
    }

    private static UserControl CreateInput(bool timeInput)
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-GB");
        return timeInput
            ? new TimeUpDown { TimeValue = TimeSpan.FromHours(21) }
            : new NumericUpDown
            {
                Maximum = 5000,
                NumericValue = 300
            };
    }

    private static void PreviousValueIsUnchanged(UserControl control)
    {
        if (control is TimeUpDown time)
        {
            time.TimeValue.Should().Be(TimeSpan.FromHours(21));
        }
        else
        {
            ((NumericUpDown)control).NumericValue.Should().Be(300);
        }
    }
}
