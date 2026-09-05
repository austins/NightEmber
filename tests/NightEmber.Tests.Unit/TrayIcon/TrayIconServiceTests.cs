using H.NotifyIcon;
using H.NotifyIcon.Core;
using NightEmber.TrayIcon;
using System.Windows;
using System.Windows.Controls;

namespace NightEmber.Tests.Unit.TrayIcon;

public sealed class TrayIconServiceTests
{
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
        var tooltip = new string('a', 100);

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().HaveLength(63);
    }

    [Fact]
    public void TrimTooltip_SurrogatePairAtLimit_DoesNotSplitThePair()
    {
        // Arrange
        var tooltip = new string('a', 62) + "\U0001F319" + new string('b', 20);

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().HaveLength(62);
        char.IsSurrogate(result[^1]).Should().BeFalse();
    }

    [Fact]
    public void TrimTooltip_SurrogatePairFitsAtLimit_KeepsThePair()
    {
        // Arrange
        var tooltip = new string('a', 61) + "\U0001F319" + "extra";

        // Act
        var result = TrayIconService.TrimTooltip(tooltip);

        // Assert
        result.Should().Be(new string('a', 61) + "\U0001F319");
    }

    [Fact]
    public async Task TrayMenu_ActivationAndCallbacks_PreserveActionsWithoutCreatingShellIcon()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var toggles = 0;
            var settings = 0;
            var exits = 0;
            using var service = new TrayIconService(icon, () => toggles++, () => settings++, () => exits++);
            var menu = icon.ContextMenu;

            var toggle = (MenuItem)menu.Items[0];
            var showSettings = (MenuItem)menu.Items[2];
            var exit = (MenuItem)menu.Items[4];

            // Act
            toggle.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            showSettings.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            exit.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayLeftMouseUpEvent));

            // Assert
            icon.MenuActivation.Should().Be(PopupActivationMode.RightClick);
            icon.PopupActivation.Should().Be(PopupActivationMode.None);
            menu.Items.Count.Should().Be(5);
            menu.Items[1].Should().BeOfType<Separator>();
            menu.Items[3].Should().BeOfType<Separator>();
            toggle.Header.Should().Be("Turn on now");
            showSettings.Header.Should().Be("Settings...");
            exit.Header.Should().Be("Exit");
            toggles.Should().Be(1);
            settings.Should().Be(2);
            exits.Should().Be(1);
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Update_RepeatedStateChanges_KeepsBothOwnedIconsUsable()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            using var service = new TrayIconService(icon, static () => { }, static () => { }, static () => { });
            var toggle = (MenuItem)icon.ContextMenu.Items[0];
            var offHandle = icon.TrayIcon.Icon;
            var results =
                new List<(bool IsOn, object Header, string Tooltip, bool OwnsIcon, nint Handle, bool Created)>();

            // Act
            for (var index = 0; index < 4; index++)
            {
                var isOn = index % 2 == 0;
                service.Update(isOn, new string('a', 62) + "\U0001F319");

                results.Add(
                    (isOn, toggle.Header, icon.ToolTipText, icon.Icon is not null, icon.TrayIcon.Icon, icon.IsCreated));
            }

            // Assert
            offHandle.Should().NotBe(nint.Zero);
            foreach (var result in results)
            {
                result.Header.Should().Be(result.IsOn ? "Turn off now" : "Turn on now");
                result.Tooltip.Should().Be(new string('a', 62));
                // Setting Icon would transfer ownership to the library and invalidate a cached icon on the next update.
                result.OwnsIcon.Should().BeFalse();
                result.Handle.Should().NotBe(nint.Zero);
                if (result.IsOn)
                {
                    result.Handle.Should().NotBe(offHandle);
                }
                else
                {
                    result.Handle.Should().Be(offHandle);
                }

                result.Created.Should().BeFalse();
            }
        });
    }

    [Fact]
    public async Task Dispose_RepeatedDisposal_DetachesHandlersAndRejectsFurtherUpdates()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var callbacks = 0;
            using var service = new TrayIconService(icon, () => callbacks++, () => callbacks++, () => callbacks++);
            var menu = icon.ContextMenu;
            var items = menu.Items.OfType<MenuItem>().ToArray();

            // Act
            service.Dispose();
            service.Dispose();
            foreach (var item in items)
            {
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }

            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayLeftMouseUpEvent));
            var update = () => service.Update(true, "Night Ember");
            var showError = () => service.ShowError("Error");

            // Assert
            callbacks.Should().Be(0);
            icon.IsDisposed.Should().BeTrue();
            icon.ContextMenu.Should().BeNull();
            menu.IsOpen.Should().BeFalse();
            menu.Items.Count.Should().Be(0);
            menu.DataContext.Should().BeNull();
            update.Should().Throw<ObjectDisposedException>();
            showError.Should().Throw<ObjectDisposedException>();
        });
    }
}
