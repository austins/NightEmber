using NightEmber.Controls;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace NightEmber.Tests.Unit.Controls;

public sealed class SpinnerAccessibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Input_AccessibilityProperties_ExposeLabelHelpShortcutAndValue(bool timeInput)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl control = timeInput ? new TimeUpDown() : new NumericUpDown();
            var label = new Label
            {
                Content = "Test _input",
                Target = control
            };
            AutomationProperties.SetLabeledBy(control, label);
            AutomationProperties.SetHelpText(control, "Use Up and Down to adjust.");
            AutomationProperties.SetAcceleratorKey(control, "Alt+I");
            var input = Input(control);
            var host = new StackPanel();
            host.Children.Add(label);
            host.Children.Add(control);
            host.Measure(new Size(200, 100));
            host.Arrange(new Rect(0, 0, 200, 100));
            host.UpdateLayout();
            _ = UIElementAutomationPeer.CreatePeerForElement(label);

            // Act
            var peer = UIElementAutomationPeer.CreatePeerForElement(input);
            var value = (IValueProvider)peer.GetPattern(PatternInterface.Value);

            // Assert
            AutomationProperties.GetLabeledBy(input).Should().BeSameAs(label);
            peer.GetName().Should().Be("Test input");
            peer.GetHelpText().Should().Be("Use Up and Down to adjust.");
            peer.GetAcceleratorKey().Should().Be("Alt+I");
            value.Value.Should().Be(input.Text);
            value.IsReadOnly.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Input_TabNavigation_ExposesOnlyTheEditableTextBox(bool timeInput)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl control = timeInput ? new TimeUpDown() : new NumericUpDown();
            var grid = (Grid)((Border)control.Content).Child;
            var arrows = ((Grid)grid.Children[1]).Children.OfType<RepeatButton>().ToArray();

            // Act
            var tab = WpfTestHelper.PressKey(Input(control), Key.Tab);

            // Assert
            control.Focusable.Should().BeFalse();
            control.IsTabStop.Should().BeFalse();
            KeyboardNavigation.GetTabNavigation(control).Should().Be(KeyboardNavigationMode.Once);
            Input(control).IsTabStop.Should().BeTrue();
            arrows
                .Should()
                .HaveCount(2)
                .And
                .AllSatisfy(static arrow =>
                {
                    arrow.Focusable.Should().BeFalse();
                    arrow.IsTabStop.Should().BeFalse();
                });
            tab.Handled.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData("h:mm tt", "Hour selected", "Minute selected", "AM/PM selected")]
    [InlineData("HH:mm", "Hour selected", "Minute selected", "Hour selected")]
    public async Task SelectedSegment_KeyboardNavigation_UpdatesAutomationStatus(
        string pattern,
        string initial,
        string next,
        string last)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
            culture.DateTimeFormat.ShortTimePattern = pattern;
            CultureInfo.CurrentCulture = culture;
            var control = new TimeUpDown { TimeValue = TimeSpan.FromHours(21) };
            var input = Input(control);
            var peer = UIElementAutomationPeer.CreatePeerForElement(input);
            var statuses = new List<string> { peer.GetItemStatus() };

            // Act
            WpfTestHelper.PressKey(input, Key.Right);
            statuses.Add(peer.GetItemStatus());
            WpfTestHelper.PressKey(input, Key.Right);
            statuses.Add(peer.GetItemStatus());

            // Assert
            statuses.Should().Equal(initial, next, last);
        });
    }

    private static TextBox Input(UserControl control)
    {
        return (TextBox)((Grid)((Border)control.Content).Child).Children[0];
    }
}
