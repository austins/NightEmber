using System.Runtime.InteropServices;

namespace NightEmber;

/// <summary>
/// Shows modal Win32 message boxes for failures that occur outside, or before, any Avalonia window.
/// </summary>
internal static partial class MessageBox
{
    private const string Caption = "Night Ember";
    private const uint IconError = 0x10;
    private const uint IconWarning = 0x30;

    /// <summary>
    /// Shows a modal warning with an OK button.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void ShowWarning(string message)
    {
        _ = Show(0, message, Caption, IconWarning);
    }

    /// <summary>
    /// Shows a modal error with an OK button.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public static void ShowError(string message)
    {
        _ = Show(0, message, Caption, IconError);
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int Show(nint owner, string text, string caption, uint type);
}
