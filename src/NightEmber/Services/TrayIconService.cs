namespace NightEmber.Services;

/// <summary>
/// Owns the system-tray icon, context menu, and user notifications.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private const int ErrorBalloonDurationMilliseconds = 5000;

    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _toggleItem;
    private readonly ToolStripMenuItem _settingsItem;
    private readonly ToolStripMenuItem _exitItem;
    private readonly ToolStripSeparator _firstSeparator;
    private readonly ToolStripSeparator _secondSeparator;
    private readonly ContextMenuStrip _menu;
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

        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip = null;
        _notifyIcon.Dispose();
        _menu.Dispose();
        _toggleItem.Dispose();
        _settingsItem.Dispose();
        _exitItem.Dispose();
        _firstSeparator.Dispose();
        _secondSeparator.Dispose();
        _onIcon.Dispose();
        _offIcon.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService" /> class.
    /// </summary>
    /// <param name="toggle">The action for toggling the tint.</param>
    /// <param name="showSettings">The action for opening settings.</param>
    /// <param name="exit">The action for exiting the application.</param>
    public TrayIconService(Action toggle, Action showSettings, Action exit)
    {
        _onIcon = TrayIconFactory.Create(true);
        _offIcon = TrayIconFactory.Create(false);
        _toggleItem = new ToolStripMenuItem("Turn on now");
        _toggleItem.Click += (_, _) => toggle();

        _settingsItem = new ToolStripMenuItem("Settings...");
        _settingsItem.Click += (_, _) => showSettings();
        _exitItem = new ToolStripMenuItem("Exit");
        _exitItem.Click += (_, _) => exit();
        _firstSeparator = new ToolStripSeparator();
        _secondSeparator = new ToolStripSeparator();

        _menu = new ContextMenuStrip();
        _menu.Items.Add(_toggleItem);
        _menu.Items.Add(_firstSeparator);
        _menu.Items.Add(_settingsItem);
        _menu.Items.Add(_secondSeparator);
        _menu.Items.Add(_exitItem);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = _menu,
            Icon = _offIcon,
            Text = "Night Ember",
            Visible = true
        };
        _notifyIcon.MouseClick += (_, eventArgs) =>
        {
            if (eventArgs.Button == MouseButtons.Left)
            {
                showSettings();
            }
        };
    }

    /// <summary>
    /// Updates the tray icon, toggle caption, and tooltip.
    /// </summary>
    /// <param name="isOn">Whether the tint is active.</param>
    /// <param name="tooltip">The shell tooltip text.</param>
    public void Update(bool isOn, string tooltip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _notifyIcon.Icon = isOn ? _onIcon : _offIcon;
        _toggleItem.Text = isOn ? "Turn off now" : "Turn on now";
        _notifyIcon.Text = TrimTooltip(tooltip);
    }

    /// <summary>
    /// Displays an error balloon from the tray icon.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    public void ShowError(string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _notifyIcon.ShowBalloonTip(ErrorBalloonDurationMilliseconds, "Night Ember", message, ToolTipIcon.Error);
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
}
