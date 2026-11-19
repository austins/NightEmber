using NightEmber.Core.Interop;
using System.Runtime.InteropServices;

namespace NightEmber.Core.Services;

/// <summary>
/// Manages the current user's Startup-folder shortcut.
/// </summary>
public sealed class StartupService
{
    private const string ShortcutName = "Night Ember.lnk";
    private const string HiddenArgument = "--hidden";

    // SW_SHOWMINNOACTIVE: minimized without activation (same value as WshWindowStyle 7).
    private const int MinimizedWindowStyle = 7;
    private const uint StgmRead = 0;
    private const int PathBufferLength = 520;
    private const int ArgumentsBufferLength = 1024;

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
            var link = ShellLinkInterop.CreateShellLink();
            try
            {
                ((IPersistFile)link).Load(_shortcutPath, StgmRead);
                Span<char> targetBuffer = stackalloc char[PathBufferLength];
                Span<char> argumentsBuffer = stackalloc char[ArgumentsBufferLength];
                link.GetPath(ref MemoryMarshal.GetReference(targetBuffer), targetBuffer.Length, 0, 0);
                link.GetArguments(ref MemoryMarshal.GetReference(argumentsBuffer), argumentsBuffer.Length);
                target = ToString(targetBuffer);
                arguments = ToString(argumentsBuffer);
            }
            finally
            {
                ShellLinkInterop.Release(link);
            }
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

        var link = ShellLinkInterop.CreateShellLink();
        try
        {
            link.SetPath(executablePath);
            link.SetArguments(HiddenArgument);
            link.SetWorkingDirectory(workingDirectory);
            link.SetDescription("Night Ember");
            link.SetShowCmd(MinimizedWindowStyle);
            ((IPersistFile)link).Save(_shortcutPath, true);
        }
        finally
        {
            ShellLinkInterop.Release(link);
        }
    }

    private static string ToString(ReadOnlySpan<char> buffer)
    {
        var end = buffer.IndexOf('\0');
        return (end < 0 ? buffer : buffer[..end]).ToString();
    }
}
