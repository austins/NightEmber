using Avalonia.Controls;
using NightEmber.TrayIcon;

namespace NightEmber.Tests.Unit.TrayIcon;

public sealed class TrayIconServiceTests
{
    [Fact]
    public async Task Constructor_CreatesMenuWithAccessKeysAndInitialTooltip()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Act
            using var service = new TrayIconService(() => { }, () => { }, () => { });

            // Assert
            service.NotifyIcon.ToolTipText.Should().Be("Night Ember");
            service.NotifyIcon.Icon.Should().NotBeNull();
            service.NotifyIcon.Menu!.Items.Should().HaveCount(5);
            service.NotifyIcon.Menu.Items.Select(item => item is NativeMenuItemSeparator ? "-" : ((NativeMenuItem)item).Header)
                .Should().Equal("_Turn on now", "-", "_Settings...", "-", "E_xit");
        });
    }

    [Fact]
    public async Task MenuItems_Clicked_InvokeTheMatchingActions()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var calls = new List<string>();
            using var service = new TrayIconService(
                () => calls.Add("toggle"),
                () => calls.Add("settings"),
                () => calls.Add("exit"));
            var items = service.NotifyIcon.Menu!.Items;

            // Act
#pragma warning disable CS0618 // Only the implementation assembly marks this unstable; NativeMenuItem has no stable way to raise Click.
            ((INativeMenuItemExporterEventsImplBridge)items[0]).RaiseClicked();
            ((INativeMenuItemExporterEventsImplBridge)items[2]).RaiseClicked();
            ((INativeMenuItemExporterEventsImplBridge)items[4]).RaiseClicked();
#pragma warning restore CS0618

            // Assert
            calls.Should().Equal("toggle", "settings", "exit");
        });
    }

    [Theory]
    [InlineData(true, "_Turn off now")]
    [InlineData(false, "_Turn on now")]
    public async Task Update_State_SwapsIconToggleCaptionAndTooltip(bool isOn, string caption)
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            using var service = new TrayIconService(() => { }, () => { }, () => { });
            var initialIcon = service.NotifyIcon.Icon;

            // Act
            service.Update(true, "On");
            var onIcon = service.NotifyIcon.Icon;
            service.Update(isOn, "State");

            // Assert
            onIcon.Should().NotBeSameAs(initialIcon);
            service.NotifyIcon.ToolTipText.Should().Be("State");
            ((NativeMenuItem)service.NotifyIcon.Menu!.Items[0]).Header.Should().Be(caption);
            (service.NotifyIcon.Icon == onIcon).Should().Be(isOn);
        });
    }

    [Fact]
    public async Task Update_UnchangedState_KeepsTheCurrentIconAndTooltip()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            using var service = new TrayIconService(() => { }, () => { }, () => { });
            service.Update(true, "Same");
            var icon = service.NotifyIcon.Icon;

            // Act
            service.Update(true, "Same");

            // Assert
            service.NotifyIcon.Icon.Should().BeSameAs(icon);
            service.NotifyIcon.ToolTipText.Should().Be("Same");
        });
    }

    [Fact]
    public async Task Update_LongTooltip_IsTrimmedToTheShellLimit()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            using var service = new TrayIconService(() => { }, () => { }, () => { });

            // Act
            service.Update(true, new string('a', 200));

            // Assert
            service.NotifyIcon.ToolTipText.Should().HaveLength(127);
        });
    }

    [Fact]
    public async Task Dispose_RepeatedDisposal_IsIdempotentAndRejectsFurtherUpdates()
    {
        await AvaloniaTestHelper.RunAsync(() =>
        {
            // Arrange
            var service = new TrayIconService(() => { }, () => { }, () => { });

            // Act
            service.Dispose();
            var second = Record.Exception(service.Dispose);
            var update = Record.Exception(() => service.Update(true, "After"));

            // Assert
            second.Should().BeNull();
            update.Should().BeOfType<ObjectDisposedException>();
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("Night Ember off")]
    public void TrimTooltip_ShortText_ReturnsOriginalText(string tooltip)
    {
        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().Be(tooltip);
    }

    [Fact]
    public void TrimTooltip_LongText_TrimsToShellLimit()
    {
        // Arrange
        var tooltip = new string('a', 200);

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().HaveLength(127);
    }

    [Fact]
    public void TrimTooltip_SurrogatePairAtLimit_DoesNotSplitThePair()
    {
        // Arrange
        var tooltip = new string('a', 126) + "\U0001F319" + new string('b', 20);

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().HaveLength(126);
        char.IsSurrogate(result[^1]).Should().BeFalse();
    }

    [Fact]
    public void TrimTooltip_SurrogatePairFitsAtLimit_KeepsThePair()
    {
        // Arrange
        var tooltip = new string('a', 125) + "\U0001F319" + "extra";

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().Be(new string('a', 125) + "\U0001F319");
    }
}
