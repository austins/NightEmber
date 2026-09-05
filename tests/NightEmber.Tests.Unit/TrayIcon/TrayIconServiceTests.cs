using H.NotifyIcon;
using H.NotifyIcon.Core;
using NightEmber.TrayIcon;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace NightEmber.Tests.Unit.TrayIcon;

public sealed class TrayIconServiceTests
{
    [Fact]
    public async Task TrayMenu_IdleWarmup_PreparesLayoutWithoutOpeningOrChangingFocus()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            using var service = new TrayIconService(icon, static () => { }, static () => { }, static () => { });
            var menu = icon.ContextMenu;
            menu.Resources.MergedDictionaries.Add(
                new ResourceDictionary
                {
                    Source = new Uri(
                        "/PresentationFramework.Fluent;component/Themes/Fluent.Dark.xaml",
                        UriKind.Relative)
                });
            var opened = 0;
            menu.Opened += (_, _) => opened++;
            var originalFocus = Keyboard.FocusedElement;
            var initialSize = menu.DesiredSize;

            // Act
            ProcessIdleWork(menu.Dispatcher);

            // Assert
            initialSize.Should().Be(default(Size));
            menu.DesiredSize.Width.Should().BeGreaterThan(0);
            menu.DesiredSize.Height.Should().BeGreaterThan(0);
            menu.IsMeasureValid.Should().BeTrue();
            menu.IsArrangeValid.Should().BeTrue();
            menu.Items.OfType<Control>().Should().OnlyContain(item => item.Template != null);
            opened.Should().Be(0);
            menu.IsOpen.Should().BeFalse();
            PresentationSource.FromVisual(menu).Should().BeNull();
            Keyboard.FocusedElement.Should().BeSameAs(originalFocus);
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TrayMenu_OpenedOrDisposedBeforeIdle_CancelsWarmup(bool dispose)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            using var service = new TrayIconService(icon, static () => { }, static () => { }, static () => { });
            var menu = icon.ContextMenu;

            // Act
            if (dispose)
            {
                service.Dispose();
            }
            else
            {
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
                menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));
            }

            ProcessIdleWork(menu.Dispatcher);

            // Assert
            menu.DesiredSize.Should().Be(default(Size));
            menu.IsOpen.Should().BeFalse();
            PresentationSource.FromVisual(menu).Should().BeNull();
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task TrayMenu_ChangedTheme_UpdatesWithoutCreatingShellIcon()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            using var service = new TrayIconService(icon, static () => { }, static () => { }, static () => { });
            var menu = icon.ContextMenu;
            var applicationResources = new ResourceDictionary();
            menu.Resources.MergedDictionaries.Add(applicationResources);
            var backgrounds = new List<System.Windows.Media.Brush>();
            string[] themes = ["Light", "Dark", "HC"];

            // Act
            ProcessIdleWork(menu.Dispatcher);
            foreach (var theme in themes)
            {
                applicationResources.MergedDictionaries.Clear();
                applicationResources.MergedDictionaries.Add(
                    new ResourceDictionary
                    {
                        Source = new Uri(
                            $"/PresentationFramework.Fluent;component/Themes/Fluent.{theme}.xaml",
                            UriKind.Relative)
                    });
                menu.ApplyTemplate();
                backgrounds.Add(menu.Background);

                // Assert
                menu.Background.Should().BeSameAs(menu.FindResource("ContextMenuBackground"));
                menu.Foreground.Should().BeSameAs(menu.FindResource("ContextMenuForeground"));
                menu.Template.Should().NotBeNull();
                icon.IsCreated.Should().BeFalse();
                menu.IsOpen.Should().BeFalse();
            }

            backgrounds[0].Should().NotBeSameAs(backgrounds[1]);
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
            toggle.Header.Should().Be("_Turn on now");
            showSettings.Header.Should().Be("_Settings...");
            exit.Header.Should().Be("E_xit");
            new MenuItemAutomationPeer(toggle).GetName().Should().Be("Turn on now");
            new MenuItemAutomationPeer(showSettings).GetName().Should().Be("Settings...");
            new MenuItemAutomationPeer(exit).GetName().Should().Be("Exit");
            AutomationProperties.GetName(menu).Should().Be("Night Ember");
            AutomationProperties.GetName(icon).Should().Be("Night Ember");
            AutomationProperties.GetItemStatus(icon).Should().Be("Tint off");
            toggles.Should().Be(1);
            settings.Should().Be(2);
            exits.Should().Be(1);
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task TrayKeyboardKeySelect_KeyboardAndMouseNotifications_ActivateSettingsOncePerInput()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var settings = 0;
            using var service = new TrayIconService(icon, static () => { }, () => settings++, static () => { });

            // Act
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardKeySelectEvent));
            var keyboardActivations = settings;
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayLeftMouseUpEvent));
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardSelectEvent));

            // Assert
            keyboardActivations.Should().Be(1);
            settings.Should().Be(2);
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task TrayKeyboardContextMenu_OpenAndDismiss_FocusesFirstItemAndReturnsToTrayOnce()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var opened = 0;
            var focused = 0;
            var restored = 0;
            using var service = new TrayIconService(
                icon,
                static () => { },
                static () => { },
                static () => { },
                () =>
                {
                    opened++;
                    return true;
                },
                () => focused++,
                () => restored++);
            var menu = icon.ContextMenu;

            // Act
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardContextMenuEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardContextMenuEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));

            // Assert
            opened.Should().Be(1);
            focused.Should().Be(1);
            restored.Should().Be(1);
            menu.IsOpen.Should().BeFalse();
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task TrayKeyboardContextMenu_OpeningCancelled_AllowsRetryWithoutReturningFocus()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var requests = 0;
            var focusChanges = 0;
            using var service = new TrayIconService(
                icon,
                static () => { },
                static () => { },
                static () => { },
                () =>
                {
                    requests++;
                    return false;
                },
                () => focusChanges++,
                () => focusChanges++);
            var menu = icon.ContextMenu;

            // Act
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardContextMenuEvent));
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardContextMenuEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));

            // Assert
            requests.Should().Be(2);
            focusChanges.Should().Be(0);
            icon.IsCreated.Should().BeFalse();
            menu.IsOpen.Should().BeFalse();
        });
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public async Task TrayKeyboardContextMenu_SettingsOrExitSelected_DoesNotStealFocusAfterAction(int itemIndex)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var callbacks = 0;
            var restored = 0;
            using var service = new TrayIconService(
                icon,
                static () => { },
                () => callbacks++,
                () => callbacks++,
                static () => true,
                static () => { },
                () => restored++);
            var menu = icon.ContextMenu;

            // Act
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardContextMenuEvent));
            ((MenuItem)menu.Items[itemIndex]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));

            // Assert
            callbacks.Should().Be(1);
            restored.Should().Be(0);
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task TrayMenu_MousePopupLifecycle_DoesNotChangeKeyboardFocus()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var focusChanges = 0;
            using var service = new TrayIconService(
                icon,
                static () => { },
                static () => { },
                static () => { },
                static () => true,
                () => focusChanges++,
                () => focusChanges++);
            var menu = icon.ContextMenu;

            // Act
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));

            // Assert
            focusChanges.Should().Be(0);
            icon.IsCreated.Should().BeFalse();
            menu.IsOpen.Should().BeFalse();
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
                result.Header.Should().Be(result.IsOn ? "_Turn off now" : "_Turn on now");
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

    [Theory]
    [InlineData(true, "Turn off now", "Tint on")]
    [InlineData(false, "Turn on now", "Tint off")]
    public async Task Update_TintState_ExposesAccessibleActionAndStatus(bool isOn, string action, string status)
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            using var service = new TrayIconService(icon, static () => { }, static () => { }, static () => { });
            var toggle = (MenuItem)icon.ContextMenu.Items[0];

            // Act
            service.Update(isOn, "Night Ember test state");

            // Assert
            new MenuItemAutomationPeer(toggle).GetName().Should().Be(action);
            AutomationProperties.GetItemStatus(toggle).Should().Be(status);
            AutomationProperties.GetItemStatus(icon).Should().Be(status);
            icon.ToolTipText.Should().Be("Night Ember test state");
            icon.IsCreated.Should().BeFalse();
        });
    }

    [Fact]
    public async Task Dispose_KeyboardPopupRequested_CancelsFocusWithoutReturningToShell()
    {
        await WpfTestHelper.RunAsync(() =>
        {
            // Arrange
            using var icon = new TaskbarIcon();
            var focused = 0;
            var restored = 0;
            using var service = new TrayIconService(
                icon,
                static () => { },
                static () => { },
                static () => { },
                static () => true,
                () => focused++,
                () => restored++);
            var menu = icon.ContextMenu;
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardContextMenuEvent));

            // Act
            service.Dispose();
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));

            // Assert
            focused.Should().Be(0);
            restored.Should().Be(0);
            icon.IsDisposed.Should().BeTrue();
            menu.IsOpen.Should().BeFalse();
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
            using var service = new TrayIconService(
                icon,
                () => callbacks++,
                () => callbacks++,
                () => callbacks++,
                () =>
                {
                    callbacks++;
                    return true;
                },
                () => callbacks++,
                () => callbacks++);
            var menu = icon.ContextMenu;
            menu.Resources.MergedDictionaries.Add(new ResourceDictionary());
            var items = menu.Items.OfType<MenuItem>().ToArray();

            // Act
            service.Dispose();
            service.Dispose();
            foreach (var item in items)
            {
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            }

            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayLeftMouseUpEvent));
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardKeySelectEvent));
            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayKeyboardContextMenuEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));
            menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));
            var update = () => service.Update(true, "Night Ember");
            var showError = () => service.ShowError("Error");

            // Assert
            callbacks.Should().Be(0);
            icon.IsDisposed.Should().BeTrue();
            icon.ContextMenu.Should().BeNull();
            menu.IsOpen.Should().BeFalse();
            menu.Items.Count.Should().Be(0);
            menu.Resources.MergedDictionaries.Should().BeEmpty();
            menu.DataContext.Should().BeNull();
            update.Should().Throw<ObjectDisposedException>();
            showError.Should().Throw<ObjectDisposedException>();
        });
    }

    private static void ProcessIdleWork(Dispatcher dispatcher)
    {
#pragma warning disable VSTHRD001 // Explicitly drain queued preparation on the owning STA thread without sleeps.
        dispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
#pragma warning restore VSTHRD001
    }
}
