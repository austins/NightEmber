using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;

namespace NightEmber.Services;

/// <summary>
/// Records unhandled exceptions in a portable log file beside the executable.
/// </summary>
internal static class CrashLog
{
    /// <summary>
    /// The largest log size retained before the next entry starts a new file.
    /// </summary>
    internal const long MaximumBytes = 1024 * 1024;

    private static readonly string DefaultPath = Path.Combine(AppContext.BaseDirectory, "NightEmber.log");

    /// <summary>
    /// Appends an unhandled exception to the portable crash log without throwing.
    /// </summary>
    /// <param name="exception">The unhandled exception, or <see langword="null" /> when unavailable.</param>
    public static void Write(object? exception)
    {
        Write(DefaultPath, exception, DateTimeOffset.Now);
    }

    internal static void Write(string path, object? exception, DateTimeOffset timestamp)
    {
        var entry = string.Create(
            CultureInfo.InvariantCulture,
            $"[{timestamp:O}] Unhandled exception{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");

        try
        {
            var file = new FileInfo(path);
            if (file is { Exists: true, Length: >= MaximumBytes })
            {
                File.WriteAllText(path, entry);
            }
            else
            {
                File.AppendAllText(path, entry);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            // Logging is best-effort; a read-only folder must not mask the original failure.
            Trace.TraceWarning("Could not write crash log: {0}", ex.Message);
        }
    }
}
