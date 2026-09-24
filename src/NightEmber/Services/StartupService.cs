using System.IO;
using System.Runtime.InteropServices;

namespace NightEmber.Services;

/// <summary>
/// Manages the current user's Startup-folder shortcut.
/// </summary>
internal sealed class StartupService
{
    private const string ShortcutName = "Night Ember.lnk";
    private const string HiddenArgument = "--hidden";

    // WScript.Shell's WshWindowStyle value for an active, minimized window.
    private const int MinimizedWindowStyle = 7;

    private readonly string _shortcutPath;
    private readonly string? _executablePath;

    /// <summary>
    /// Initializes a new instance of the <see cref="StartupService" /> class for the current executable.
    /// </summary>
    public StartupService()
        : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), ShortcutName),
            Environment.ProcessPath)
    {
    }

    internal StartupService(string shortcutPath, string? executablePath)
    {
        _shortcutPath = shortcutPath;
        _executablePath = executablePath;
    }

    private enum ShortcutState
    {
        Missing,
        Current,
        Stale,
        OtherCopy
    }

    /// <summary>
    /// Gets a value indicating whether the startup shortcut launches this executable.
    /// </summary>
    public bool IsEnabled => GetShortcutState() == ShortcutState.Current;

    /// <summary>
    /// Creates or removes the current user's startup shortcut.
    /// </summary>
    /// <param name="enabled"><see langword="true" /> to create the shortcut; otherwise, remove it.</param>
    /// <remarks>
    /// The shortcut starts the current executable with <c>--hidden</c>. This
    /// avoids registry changes and does not require administrator privileges.
    /// An up-to-date shortcut is left untouched, and disabling startup leaves a
    /// shortcut that launches another existing copy of Night Ember in place.
    /// </remarks>
    public void SetEnabled(bool enabled)
    {
        var state = GetShortcutState();
        if (enabled)
        {
            if (state != ShortcutState.Current)
            {
                CreateShortcut();
            }

            return;
        }

        if (state is ShortcutState.Current or ShortcutState.Stale)
        {
            File.Delete(_shortcutPath);
        }
    }

    private static T UseShortcut<T>(string shortcutPath, Func<dynamic, T> action)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell", true)
                        ?? throw new InvalidOperationException("Windows Script Host is unavailable.");

        var shell = Activator.CreateInstance(shellType)
                    ?? throw new InvalidOperationException("Windows Script Host could not be started.");

        object? shortcut = null;

        try
        {
            // Dynamic avoids adding an IWshRuntimeLibrary interop dependency solely
            // to manage this startup shortcut through WScript.Shell.
            dynamic dynamicShell = shell;
            shortcut = dynamicShell.CreateShortcut(shortcutPath);
            return action(shortcut);
        }
        finally
        {
            if (shortcut is not null && Marshal.IsComObject(shortcut))
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (Marshal.IsComObject(shell))
            {
                Marshal.FinalReleaseComObject(shell);
            }
        }
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    }

    private ShortcutState GetShortcutState()
    {
        if (!File.Exists(_shortcutPath))
        {
            return ShortcutState.Missing;
        }

        string target;
        string arguments;
        try
        {
            (target, arguments) = UseShortcut(
                _shortcutPath,
                static shortcut => ((string)shortcut.TargetPath, (string)shortcut.Arguments));
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or IOException
                                       or UnauthorizedAccessException)
        {
            return ShortcutState.Stale;
        }

        if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
        {
            return ShortcutState.Stale;
        }

        if (_executablePath is null || !PathsEqual(target, _executablePath))
        {
            return ShortcutState.OtherCopy;
        }

        // A shortcut to this executable with different arguments predates the current format.
        return string.Equals(arguments, HiddenArgument, StringComparison.Ordinal)
            ? ShortcutState.Current
            : ShortcutState.Stale;
    }

    private void CreateShortcut()
    {
        var executablePath = _executablePath
                             ?? throw new InvalidOperationException("The executable path is unavailable.");
        var workingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory;

        UseShortcut(
            _shortcutPath,
            shortcut =>
            {
                shortcut.TargetPath = executablePath;
                shortcut.Arguments = HiddenArgument;
                shortcut.WorkingDirectory = workingDirectory;
                shortcut.Description = "Night Ember";
                shortcut.WindowStyle = MinimizedWindowStyle;
                shortcut.Save();
                return true;
            });
    }
}
