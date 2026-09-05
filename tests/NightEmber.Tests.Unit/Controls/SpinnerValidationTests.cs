using NightEmber.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;

namespace NightEmber.Tests.Unit.Controls;

public sealed class SpinnerValidationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LostFocus_InvalidEdit_PreservesTextAndExposesAccessibleError(bool timeInput)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var input = Input(control);
            var peer = UIElementAutomationPeer.CreatePeerForElement(input);
            input.Text = "invalid";
            var errorBeforeCommit = InputValidation.GetHasError(control);

            // Act
            input.RaiseEvent(
                new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, null)
                {
                    RoutedEvent = Keyboard.LostKeyboardFocusEvent
                });
            var committedAgain = Commit(control);

            // Assert
            errorBeforeCommit.Should().BeFalse();
            input.Text.Should().Be("invalid");
            committedAgain.Should().BeFalse();
            InputValidation.GetHasError(control).Should().BeTrue();
            peer.GetItemStatus().Should().Be(InputValidation.GetErrorMessage(control));
            peer.GetItemStatus().Should().NotBeNullOrWhiteSpace();
            PreviousValueIsUnchanged(control);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Correction_AfterValidation_UpdatesErrorWithoutDiscardingText(bool timeInput)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var input = Input(control);
            input.Text = "invalid";
            Commit(control);

            // Act
            input.Text = timeInput ? "09:45" : "5000";
            var corrected = InputValidation.GetHasError(control);
            input.Text = timeInput ? "99:45" : "5001";
            var invalidAgain = InputValidation.GetHasError(control);
            input.Text = timeInput ? "09:45" : "5000";
            var committed = Commit(control);

            // Assert
            corrected.Should().BeFalse();
            invalidAgain.Should().BeTrue();
            committed.Should().BeTrue();
            InputValidation.GetHasError(control).Should().BeFalse();
            InputValidation.GetErrorMessage(control).Should().BeEmpty();
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
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var input = Input(control);
            input.Text = "invalid";

            // Act
            var args = WpfTestHelper.PressKey(input, key);

            // Assert
            args.Handled.Should().BeTrue();
            input.Text.Should().Be("invalid");
            InputValidation.GetHasError(control).Should().BeTrue();
            PreviousValueIsUnchanged(control);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Escape_InvalidEdit_RestoresValueAndClearsError(bool timeInput)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            var input = Input(control);
            input.Text = "invalid";
            Commit(control);

            // Act
            WpfTestHelper.PressKey(input, Key.Escape);

            // Assert
            input.Text.Should().Be(timeInput ? "21:00" : "300");
            InputValidation.GetHasError(control).Should().BeFalse();
            AutomationProperties.GetItemStatus(input).Should().Be(timeInput ? "Hour selected" : string.Empty);
            PreviousValueIsUnchanged(control);
        });
    }

    [Theory]
    [InlineData(Key.Left)]
    [InlineData(Key.Right)]
    public async Task HorizontalArrows_PendingTimeEdit_AllowNormalCaretNavigation(Key key)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(true);
            var input = Input(control);
            input.Text = "1212:00 PM";

            // Act
            var args = WpfTestHelper.PressKey(input, key);

            // Assert
            args.Handled.Should().BeFalse();
            input.Text.Should().Be("1212:00 PM");
        });
    }

    [Theory]
    [InlineData(false, "Light")]
    [InlineData(false, "Dark")]
    [InlineData(false, "HC")]
    [InlineData(true, "Light")]
    [InlineData(true, "Dark")]
    [InlineData(true, "HC")]
    public async Task InvalidEdit_ThemeOutline_UsesCriticalBrushWithoutMovingContent(bool timeInput, string theme)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var control = CreateInput(timeInput);
            control.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri(
                        $"/PresentationFramework.Fluent;component/Themes/Fluent.{theme}.xaml",
                        UriKind.Relative)
                });
            var border = (Border)control.Content;
            Layout(control);
            var before = border.Child.RenderSize;
            Input(control).Text = "invalid";

            // Act
            Commit(control);
            Layout(control);
            var errorBrush = border.BorderBrush;
            var after = border.Child.RenderSize;
            control.IsEnabled = false;
            Layout(control);

            // Assert
            errorBrush.Should().BeSameAs(control.FindResource("SystemFillColorCriticalBrush"));
            after.Should().Be(before);
            border.BorderBrush.Should().BeSameAs(control.FindResource("ControlStrongStrokeColorDefaultBrush"));
            InputValidation.GetHasError(control).Should().BeTrue();
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

    private static TextBox Input(UserControl control)
    {
        return (TextBox)control.FindName("ValueTextBox");
    }

    private static bool Commit(UserControl control)
    {
        return control is TimeUpDown time ? time.CommitEdit() : ((NumericUpDown)control).CommitEdit();
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

    private static void Layout(FrameworkElement control)
    {
        control.Measure(new Size(110, 28));
        control.Arrange(new Rect(0, 0, 110, 28));
        control.UpdateLayout();
    }
}
