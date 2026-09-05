using H.NotifyIcon;
using H.NotifyIcon.Core;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;

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
    private readonly Icon _onIcon;
    private readonly Icon _offIcon;
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
        _toggleItem.Click -= OnToggle;
        _settingsItem.Click -= OnShowSettings;
        _exitItem.Click -= OnExit;
        _menu.IsOpen = false;
        _notifyIcon.ContextMenu = null;
        _menu.Items.Clear();
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
    internal TrayIconService(TaskbarIcon notifyIcon, Action toggle, Action showSettings, Action exit)
    {
        _notifyIcon = notifyIcon;
        _toggle = toggle;
        _showSettings = showSettings;
        _exit = exit;
        _toggleItem = new MenuItem { Header = "Turn on now" };
        _settingsItem = new MenuItem { Header = "Settings..." };
        _exitItem = new MenuItem { Header = "Exit" };
        _menu = new ContextMenu();
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
        _toggleItem.Header = isOn ? "Turn off now" : "Turn on now";
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
        _showSettings();
    }

    private void OnExit(object sender, RoutedEventArgs e)
    {
        _exit();
    }
}
