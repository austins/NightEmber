using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using NightEmber.Controls;
using System.Globalization;
using static NightEmber.Tests.Unit.Controls.SpinnerTestHelper;
using NumericUpDown = NightEmber.Controls.NumericUpDown;

namespace NightEmber.Tests.Unit.Controls;

public sealed class SpinnerAccessibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Input_ArrowButtons_ExposeIncreaseAndDecreaseNames(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl control = timeInput ? new TimeUpDown() : new NumericUpDown();
            var window = AvaloniaTestHelper.ShowInWindow(control);

            // Act
            var names = (from increase in (bool[])[true, false]
                select ControlAutomationPeer.CreatePeerForElement(ArrowButton(control, increase)).GetName()).ToArray();

            // Assert
            names.Should().Equal("Increase", "Decrease");
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Input_AccessibilityProperties_ExposeLabelHelpShortcutAndValue(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
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
            var window = AvaloniaTestHelper.ShowInWindow(host);

            // Act
            var peer = ControlAutomationPeer.CreatePeerForElement(input);
            var value = peer.GetProvider<IValueProvider>()!;

            // Assert
            AutomationProperties.GetLabeledBy(input).Should().BeSameAs(label);
            peer.GetName().Should().Be("Test input");
            peer.GetHelpText().Should().Be("Use Up and Down to adjust.");
            peer.GetAcceleratorKey().Should().Be("Alt+I");
            value.Value.Should().Be(input.Text);
            value.IsReadOnly.Should().BeFalse();
            window.Close();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Input_TabNavigation_ExposesOnlyTheEditableTextBox(bool timeInput)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            UserControl control = timeInput ? new TimeUpDown() : new NumericUpDown();
            var window = ShowWithNeighbor(control, out var other);
            Focus(control);
            var arrows = new[] { ArrowButton(control, true), ArrowButton(control, false) };

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Tab);
            var afterTab = window.FocusManager.GetFocusedElement();
            AvaloniaTestHelper.PressKey(window, Key.Tab, KeyModifiers.Shift);
            var afterShiftTab = window.FocusManager.GetFocusedElement();

            // Assert
            control.IsTabStop.Should().BeFalse();
            Input(control).IsTabStop.Should().BeTrue();
            arrows.Should().AllSatisfy(static arrow => arrow.IsTabStop.Should().BeFalse());
            afterTab.Should().BeSameAs(other);
            afterShiftTab.Should().BeSameAs(Input(control));
            window.Close();
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
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
            culture.DateTimeFormat.ShortTimePattern = pattern;
            CultureInfo.CurrentCulture = culture;
            var control = new TimeUpDown { TimeValue = TimeSpan.FromHours(21) };
            var window = AvaloniaTestHelper.ShowInWindow(control);
            Focus(control);
            var input = Input(control);
            var peer = ControlAutomationPeer.CreatePeerForElement(input);
            var statuses = new List<string?> { peer.GetItemStatus() };

            // Act
            AvaloniaTestHelper.PressKey(window, Key.Right);
            statuses.Add(peer.GetItemStatus());
            AvaloniaTestHelper.PressKey(window, Key.Right);
            statuses.Add(peer.GetItemStatus());

            // Assert
            statuses.Should().Equal(initial, next, last);
            window.Close();
        });
    }
}
