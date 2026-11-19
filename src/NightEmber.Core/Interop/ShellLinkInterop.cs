using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace NightEmber.Core.Interop;

/// <summary>
/// Declares the shell link COM interop used to manage <c>.lnk</c> shortcuts without reflection or runtime COM support.
/// </summary>
internal static partial class ShellLinkInterop
{
    private const uint ClsctxInprocServer = 1;
    private static readonly Guid ShellLinkClassId = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid UnknownInterfaceId = new("00000000-0000-0000-C000-000000000046");

    /// <summary>
    /// Creates a new in-process shell link object.
    /// </summary>
    /// <returns>A uniquely wrapped shell link; pass it to <see cref="Release" /> when done.</returns>
    internal static IShellLinkW CreateShellLink()
    {
        var classId = ShellLinkClassId;
        var interfaceId = UnknownInterfaceId;
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref classId, 0, ClsctxInprocServer, ref interfaceId, out var pointer));

        try
        {
            return (IShellLinkW)new StrategyBasedComWrappers().GetOrCreateObjectForComInstance(
                pointer,
                CreateObjectFlags.UniqueInstance);
        }
        finally
        {
            Marshal.Release(pointer);
        }
    }

    /// <summary>
    /// Releases a shell link created by <see cref="CreateShellLink" /> without waiting for finalization.
    /// </summary>
    /// <param name="link">The shell link to release.</param>
    internal static void Release(IShellLinkW link)
    {
        ((ComObject)(object)link).FinalRelease();
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(
        ref Guid classId,
        nint outer,
        uint classContext,
        ref Guid interfaceId,
        out nint instance);
}

/// <summary>
/// Reads and writes the properties of a shell shortcut. Vtable order matters: slots must stay in declared order.
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal partial interface IShellLinkW
{
    /// <summary>
    /// Reads the shortcut target path.
    /// </summary>
    /// <param name="file">The first element of the buffer that receives the path.</param>
    /// <param name="maxChars">The buffer length in characters.</param>
    /// <param name="findData">An optional <c>WIN32_FIND_DATAW</c> pointer; zero when unused.</param>
    /// <param name="flags"><c>SLGP_*</c> flags.</param>
    public void GetPath(ref char file, int maxChars, nint findData, uint flags);

    /// <summary>
    /// Gets the target item ID list. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="idList">Receives the item ID list.</param>
    public void GetIDList(out nint idList);

    /// <summary>
    /// Sets the target item ID list. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="idList">The item ID list.</param>
    public void SetIDList(nint idList);

    /// <summary>
    /// Reads the shortcut description. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="name">The first element of the buffer that receives the description.</param>
    /// <param name="maxChars">The buffer length in characters.</param>
    public void GetDescription(ref char name, int maxChars);

    /// <summary>
    /// Sets the shortcut description.
    /// </summary>
    /// <param name="name">The description.</param>
    public void SetDescription(string name);

    /// <summary>
    /// Reads the working directory. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="dir">The first element of the buffer that receives the directory.</param>
    /// <param name="maxChars">The buffer length in characters.</param>
    public void GetWorkingDirectory(ref char dir, int maxChars);

    /// <summary>
    /// Sets the working directory.
    /// </summary>
    /// <param name="dir">The directory.</param>
    public void SetWorkingDirectory(string dir);

    /// <summary>
    /// Reads the command-line arguments.
    /// </summary>
    /// <param name="args">The first element of the buffer that receives the arguments.</param>
    /// <param name="maxChars">The buffer length in characters.</param>
    public void GetArguments(ref char args, int maxChars);

    /// <summary>
    /// Sets the command-line arguments.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public void SetArguments(string args);

    /// <summary>
    /// Gets the hotkey. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="hotkey">Receives the hotkey.</param>
    public void GetHotkey(out ushort hotkey);

    /// <summary>
    /// Sets the hotkey. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="hotkey">The hotkey.</param>
    public void SetHotkey(ushort hotkey);

    /// <summary>
    /// Gets the initial show command. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="showCmd">Receives the <c>SW_*</c> value.</param>
    public void GetShowCmd(out int showCmd);

    /// <summary>
    /// Sets the initial show command.
    /// </summary>
    /// <param name="showCmd">An <c>SW_*</c> value.</param>
    public void SetShowCmd(int showCmd);

    /// <summary>
    /// Reads the icon location. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="iconPath">The first element of the buffer that receives the icon path.</param>
    /// <param name="maxChars">The buffer length in characters.</param>
    /// <param name="icon">Receives the icon index.</param>
    public void GetIconLocation(ref char iconPath, int maxChars, out int icon);

    /// <summary>
    /// Sets the icon location. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="iconPath">The icon path.</param>
    /// <param name="icon">The icon index.</param>
    public void SetIconLocation(string iconPath, int icon);

    /// <summary>
    /// Sets the relative path. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="relativePath">The relative path.</param>
    /// <param name="reserved">Must be zero.</param>
    public void SetRelativePath(string relativePath, uint reserved);

    /// <summary>
    /// Resolves a moved target. Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="window">The owner window handle.</param>
    /// <param name="flags"><c>SLR_*</c> flags.</param>
    public void Resolve(nint window, uint flags);

    /// <summary>
    /// Sets the shortcut target path.
    /// </summary>
    /// <param name="file">The target path.</param>
    public void SetPath(string file);
}

/// <summary>
/// Loads and saves a shell link object to a file. Vtable order matters: slots must stay in declared order.
/// </summary>
[GeneratedComInterface(StringMarshalling = StringMarshalling.Utf16)]
[Guid("0000010B-0000-0000-C000-000000000046")]
internal partial interface IPersistFile
{
    /// <summary>
    /// Gets the class ID (inherited from <c>IPersist</c>). Unused; reserves the vtable slot.
    /// </summary>
    /// <param name="classId">Receives the class ID.</param>
    public void GetClassID(out Guid classId);

    /// <summary>
    /// Checks for unsaved changes. Unused; reserves the vtable slot.
    /// </summary>
    /// <returns><c>S_OK</c> when dirty; otherwise <c>S_FALSE</c>.</returns>
    [PreserveSig]
    public int IsDirty();

    /// <summary>
    /// Loads the shortcut from a file.
    /// </summary>
    /// <param name="fileName">The shortcut path.</param>
    /// <param name="mode"><c>STGM_*</c> access flags.</param>
    public void Load(string fileName, uint mode);

    /// <summary>
    /// Saves the shortcut to a file.
    /// </summary>
    /// <param name="fileName">The shortcut path, or <see langword="null" /> for the current file.</param>
    /// <param name="remember">Whether the path becomes the current file.</param>
    public void Save(string? fileName, [MarshalAs(UnmanagedType.Bool)] bool remember);
}
