using System.IO;
using System.Runtime.InteropServices;

namespace NightEmber.Services;

/// <summary>
/// Manages the current user's Startup-folder shortcut.
/// </summary>
internal sealed class StartupService
{
    private const string ShortcutName = "Night Ember.lnk";

    // WScript.Shell's WshWindowStyle value for an active, minimized window.
    private const int MinimizedWindowStyle = 7;

    /// <summary>
    /// Gets a value indicating whether the startup shortcut currently exists.
    /// </summary>
    public bool IsEnabled => File.Exists(ShortcutPath);

    /// <summary>
    /// Gets the full path to the Night Ember startup shortcut.
    /// </summary>
    private string ShortcutPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Startup),
        ShortcutName);

    /// <summary>
    /// Creates or removes the current user's startup shortcut.
    /// </summary>
    /// <param name="enabled"><see langword="true" /> to create the shortcut; otherwise, remove it.</param>
    /// <remarks>
    /// The shortcut starts the current executable with <c>--hidden</c>. This
    /// avoids registry changes and does not require administrator privileges.
    /// </remarks>
    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (File.Exists(ShortcutPath))
            {
                File.Delete(ShortcutPath);
            }

            return;
        }

        var executablePath = Environment.ProcessPath
                             ?? throw new InvalidOperationException("The executable path is unavailable.");

        var shellType = Type.GetTypeFromProgID("WScript.Shell", true)
                        ?? throw new InvalidOperationException("Windows Script Host is unavailable.");

        var shell = Activator.CreateInstance(shellType)
                    ?? throw new InvalidOperationException("Windows Script Host could not be started.");

        object? shortcut = null;

        try
        {
            // Dynamic avoids adding an IWshRuntimeLibrary interop dependency solely
            // to create this startup shortcut through WScript.Shell.
            dynamic dynamicShell = shell;
            shortcut = dynamicShell.CreateShortcut(ShortcutPath);
            dynamic dynamicShortcut = shortcut;
            dynamicShortcut.TargetPath = executablePath;
            dynamicShortcut.Arguments = "--hidden";
            dynamicShortcut.WorkingDirectory = AppContext.BaseDirectory;
            dynamicShortcut.Description = "Night Ember";
            dynamicShortcut.WindowStyle = MinimizedWindowStyle;
            dynamicShortcut.Save();
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
}
