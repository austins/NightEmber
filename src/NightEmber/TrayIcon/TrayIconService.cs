using H.NotifyIcon;
using H.NotifyIcon.Core;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;

namespace NightEmber.TrayIcon;

/// <summary>
/// Owns the system-tray icon, context menu, and user notifications.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private const int ErrorBalloonDurationMilliseconds = 5000;

#pragma warning disable IDISP008 // Both constructors transfer ownership of the taskbar icon to this service.
    private readonly TaskbarIcon _notifyIcon;
#pragma warning restore IDISP008
    private readonly MenuItem _toggleItem;
    private readonly MenuItem _settingsItem;
    private readonly MenuItem _exitItem;
    private readonly ContextMenu _menu;
    private readonly Action _toggle;
    private readonly Action _showSettings;
    private readonly Action _exit;
    private readonly Func<bool> _openKeyboardMenu;
    private readonly Action _focusFirstMenuItem;
    private readonly Action _restoreTrayFocus;
    private readonly Icon _onIcon;
    private readonly Icon _offIcon;
    private DispatcherOperation? _focusOperation;
    private bool _keyboardMenuOpen;
    private bool _disposed;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.TrayLeftMouseUp -= OnShowSettings;
        _notifyIcon.TrayKeyboardKeySelect -= OnShowSettings;
        _notifyIcon.TrayKeyboardContextMenu -= OnKeyboardContextMenu;
        _menu.Opened -= OnMenuOpened;
        _menu.Closed -= OnMenuClosed;
        CancelMenuFocus();
        _toggleItem.Click -= OnToggle;
        _settingsItem.Click -= OnShowSettings;
        _exitItem.Click -= OnExit;
        _menu.IsOpen = false;
        _notifyIcon.ContextMenu = null;
        _menu.Items.Clear();
        _menu.Resources.MergedDictionaries.Clear();
        _menu.ClearValue(FrameworkElement.DataContextProperty);
        _notifyIcon.Dispose();
        _onIcon?.Dispose();
        _offIcon?.Dispose();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService" /> class.
    /// </summary>
    /// <param name="toggle">The action for toggling the tint.</param>
    /// <param name="showSettings">The action for opening settings.</param>
    /// <param name="exit">The action for exiting the application.</param>
    public TrayIconService(Action toggle, Action showSettings, Action exit)
        : this(new TaskbarIcon(), toggle, showSettings, exit)
    {
        try
        {
            // Hidden launches have no visual tree to load the icon. Gamma fades must
            // retain normal scheduling rather than opt into the library's efficiency mode.
            _notifyIcon.ForceCreate(false);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Configures and takes ownership of a taskbar icon without adding it to the shell.
    /// </summary>
    /// <param name="notifyIcon">The uncreated taskbar icon to own.</param>
    /// <param name="toggle">The action for toggling the tint.</param>
    /// <param name="showSettings">The action for opening settings.</param>
    /// <param name="exit">The action for exiting the application.</param>
    /// <param name="openKeyboardMenu">An optional replacement for opening the native tray popup.</param>
    /// <param name="focusFirstMenuItem">An optional replacement for scheduling keyboard focus in the popup.</param>
    /// <param name="restoreTrayFocus">An optional replacement for returning focus to the notification area.</param>
    internal TrayIconService(
        TaskbarIcon notifyIcon,
        Action toggle,
        Action showSettings,
        Action exit,
        Func<bool>? openKeyboardMenu = null,
        Action? focusFirstMenuItem = null,
        Action? restoreTrayFocus = null)
    {
        _notifyIcon = notifyIcon;
        _toggle = toggle;
        _showSettings = showSettings;
        _exit = exit;
        _openKeyboardMenu = openKeyboardMenu ?? OpenKeyboardMenu;
        _focusFirstMenuItem = focusFirstMenuItem ?? FocusFirstMenuItem;
        _restoreTrayFocus = restoreTrayFocus ?? RestoreTrayFocus;
        _toggleItem = new MenuItem { Header = "_Turn on now" };
        _settingsItem = new MenuItem { Header = "_Settings..." };
        _exitItem = new MenuItem { Header = "E_xit" };
        _menu = new ContextMenu();
        AutomationProperties.SetName(_menu, "Night Ember");
        AutomationProperties.SetName(_toggleItem, "Turn on now");
        AutomationProperties.SetItemStatus(_toggleItem, "Tint off");
        AutomationProperties.SetName(_settingsItem, "Settings...");
        AutomationProperties.SetName(_exitItem, "Exit");
        AutomationProperties.SetName(_notifyIcon, "Night Ember");
        AutomationProperties.SetItemStatus(_notifyIcon, "Tint off");
        AutomationProperties.SetHelpText(
            _notifyIcon,
            "Press Enter to open settings, or Shift+F10 to open the Night Ember menu.");
        if (Application.Current is { } application)
        {
            // A detached tray popup needs resource-change notifications even while no settings window is open.
            _menu.Resources.MergedDictionaries.Add(application.Resources);
        }

        _menu.Items.Add(_toggleItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(new Separator());
        _menu.Items.Add(_exitItem);

        try
        {
            _onIcon = TrayIconFactory.Create(true);
            _offIcon = TrayIconFactory.Create(false);
            _toggleItem.Click += OnToggle;
            _settingsItem.Click += OnShowSettings;
            _exitItem.Click += OnExit;
            _notifyIcon.ContextMenu = _menu;
            _notifyIcon.MenuActivation = PopupActivationMode.RightClick;
            _notifyIcon.PopupActivation = PopupActivationMode.None;
            _notifyIcon.ToolTipText = "Night Ember";
            _notifyIcon.TrayLeftMouseUp += OnShowSettings;
            // NIN_SELECT also accompanies mouse activation; only NIN_KEYSELECT represents keyboard activation.
            _notifyIcon.TrayKeyboardKeySelect += OnShowSettings;
            _notifyIcon.TrayKeyboardContextMenu += OnKeyboardContextMenu;
            _menu.Opened += OnMenuOpened;
            _menu.Closed += OnMenuClosed;

            // Icon's property setter disposes the previous value. UpdateIcon borrows
            // the handle instead, so both cached moon icons remain owned by this service.
            _notifyIcon.UpdateIcon(_offIcon);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    /// <summary>
    /// Updates the tray icon, toggle caption, and tooltip.
    /// </summary>
    /// <param name="isOn">Whether the tint is active.</param>
    /// <param name="tooltip">The shell tooltip text.</param>
    public void Update(bool isOn, string tooltip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _notifyIcon.UpdateIcon(isOn ? _onIcon : _offIcon);
        _toggleItem.Header = isOn ? "_Turn off now" : "_Turn on now";
        AutomationProperties.SetName(_toggleItem, isOn ? "Turn off now" : "Turn on now");
        AutomationProperties.SetItemStatus(_toggleItem, isOn ? "Tint on" : "Tint off");
        AutomationProperties.SetItemStatus(_notifyIcon, isOn ? "Tint on" : "Tint off");
        _notifyIcon.ToolTipText = TrimTooltip(tooltip);
    }

    /// <summary>
    /// Displays an error balloon from the tray icon.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    public void ShowError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _notifyIcon.ShowNotification(
            "Night Ember",
            message,
            NotificationIcon.Error,
            respectQuietTime: false,
            timeout: TimeSpan.FromMilliseconds(ErrorBalloonDurationMilliseconds));
    }

    /// <summary>
    /// Trims a tooltip to the length accepted by the Windows shell.
    /// </summary>
    /// <param name="tooltip">The tooltip text to trim.</param>
    /// <returns>Text no longer than the shell limit.</returns>
    /// <remarks>
    /// The trim never splits a surrogate pair, which would leave an unpaired
    /// code unit in the shell tooltip.
    /// </remarks>
    public static string TrimTooltip(string tooltip)
    {
        const int maximumLength = 63;
        if (tooltip.Length <= maximumLength)
        {
            return tooltip;
        }

        var length = maximumLength;
        if (char.IsHighSurrogate(tooltip[length - 1]))
        {
            length--;
        }

        return tooltip[..length];
    }

    private void OnToggle(object sender, RoutedEventArgs e)
    {
        _toggle();
    }

    private void OnShowSettings(object sender, RoutedEventArgs e)
    {
        CancelMenuFocus();
        _showSettings();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        CancelMenuFocus();
        _exit();
    }

    private void OnKeyboardContextMenu(object sender, RoutedEventArgs e)
    {
        // WM_CONTEXTMENU can accompany a mouse right-click already handled by the library.
        if (_menu.IsOpen || _keyboardMenuOpen)
        {
            return;
        }

        _keyboardMenuOpen = true;
        if (!_openKeyboardMenu())
        {
            CancelMenuFocus();
        }
    }

    private bool OpenKeyboardMenu()
    {
        if (!_notifyIcon.IsCreated)
        {
            return false;
        }

        try
        {
            _notifyIcon.ShowContextMenu(TrayIconPosition.GetMenuPosition(_notifyIcon.TrayIcon.Id));
            return _menu.IsOpen;
        }
        catch (InvalidOperationException ex)
        {
            Trace.TraceWarning("Could not open the tray keyboard menu: {0}", ex.Message);
            return false;
        }
    }

    private void OnMenuOpened(object sender, RoutedEventArgs e)
    {
        if (_keyboardMenuOpen)
        {
            _focusFirstMenuItem();
        }
    }

    private void FocusFirstMenuItem()
    {
        // H.NotifyIcon foregrounds the popup after IsOpen raises Opened.
#pragma warning disable VSTHRD001 // Defer on the owning WPF dispatcher until the popup is foregrounded.
        _focusOperation = _menu.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            () =>
            {
                _focusOperation = null;
                if (!_disposed && _keyboardMenuOpen && _menu.IsOpen)
                {
                    _toggleItem.Focus();
                }
            });
#pragma warning restore VSTHRD001
    }

    private void OnMenuClosed(object sender, RoutedEventArgs e)
    {
        var restoreFocus = _keyboardMenuOpen;
        CancelMenuFocus();
        if (restoreFocus)
        {
            _restoreTrayFocus();
        }
    }

    private void CancelMenuFocus()
    {
        _keyboardMenuOpen = false;
        _focusOperation?.Abort();
        _focusOperation = null;
    }

    private void RestoreTrayFocus()
    {
        if (!_notifyIcon.IsCreated || _notifyIcon.IsDisposed)
        {
            return;
        }

        try
        {
            _notifyIcon.TrayIcon.SetFocus();
        }
        catch (InvalidOperationException ex)
        {
            Trace.TraceWarning("Could not restore notification-area focus: {0}", ex.Message);
        }
    }
}
