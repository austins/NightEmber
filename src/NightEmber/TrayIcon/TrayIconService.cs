using Avalonia;
using Avalonia.Controls;
using AvaloniaTrayIcon = Avalonia.Controls.TrayIcon;

namespace NightEmber.TrayIcon;

/// <summary>
/// Owns the system-tray icon, its menu, and its tooltip.
/// </summary>
internal sealed class TrayIconService : IDisposable
{
    private readonly AvaloniaTrayIcon _notifyIcon;
    private readonly NativeMenuItem _toggleItem;
    private readonly WindowIcon _onIcon;
    private readonly WindowIcon _offIcon;
    private bool? _lastIsOn;
    private string? _lastTooltip;
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Dispose();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TrayIconService" /> class and adds the icon to the shell.
    /// </summary>
    /// <param name="toggle">The action for toggling the tint.</param>
    /// <param name="showSettings">The action for opening settings.</param>
    /// <param name="exit">The action for exiting the application.</param>
    /// <exception cref="InvalidOperationException">Thrown when no Avalonia application is running.</exception>
    public TrayIconService(Action toggle, Action showSettings, Action exit)
    {
        var application = Application.Current
                          ?? throw new InvalidOperationException("The tray icon requires a running application.");

        _toggleItem = new NativeMenuItem("_Turn on now");
        _toggleItem.Click += (_, _) => toggle();
        var settingsItem = new NativeMenuItem("_Settings...");
        settingsItem.Click += (_, _) => showSettings();
        var exitItem = new NativeMenuItem("E_xit");
        exitItem.Click += (_, _) => exit();

        _onIcon = TrayIconFactory.Create(true);
        _offIcon = TrayIconFactory.Create(false);
        _notifyIcon = new AvaloniaTrayIcon
        {
            Icon = _offIcon,
            ToolTipText = "Night Ember",
            Menu =
            [
                _toggleItem,
                new NativeMenuItemSeparator(),
                settingsItem,
                new NativeMenuItemSeparator(),
                exitItem
            ]
        };
        _notifyIcon.Clicked += (_, _) => showSettings();

        // The icon reaches the shell only once it is attached to the application, which also
        // disposes it at shutdown.
        AvaloniaTrayIcon.SetIcons(application, [_notifyIcon]);
    }

    /// <summary>
    /// Gets the underlying icon so tests can inspect its menu, image, and tooltip.
    /// </summary>
    internal AvaloniaTrayIcon NotifyIcon => _notifyIcon;

    /// <summary>
    /// Updates the tray icon, toggle caption, and tooltip.
    /// </summary>
    /// <param name="isOn">Whether the tint is active.</param>
    /// <param name="tooltip">The shell tooltip text.</param>
    public void Update(bool isOn, string tooltip)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        tooltip = TrimTooltip(tooltip);

        // The schedule poll refreshes the tray every few seconds; skip redundant shell updates.
        if (_lastIsOn == isOn && string.Equals(_lastTooltip, tooltip, StringComparison.Ordinal))
        {
            return;
        }

        _lastIsOn = isOn;
        _lastTooltip = tooltip;
        _notifyIcon.Icon = isOn ? _onIcon : _offIcon;
        _toggleItem.Header = isOn ? "_Turn off now" : "_Turn on now";
        _notifyIcon.ToolTipText = tooltip;
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
        // The shell tip buffer holds 128 UTF-16 code units, including the terminator.
        const int maximumLength = 127;
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
