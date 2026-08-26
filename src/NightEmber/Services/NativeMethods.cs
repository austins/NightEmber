using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace NightEmber.Services;

internal static partial class NativeMethods
{
    private const int GammaRampElementCount = 256 * 3;

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport(
        "gdi32.dll",
        EntryPoint = "CreateDCW",
        SetLastError = true,
        StringMarshalling = StringMarshalling.Utf16)]
    public static partial nint CreateDc(string? driver, string? device, string? output, nint initData);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll", EntryPoint = "DeleteDC", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDc(nint deviceContext);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetDeviceGammaRamp(
        nint deviceContext,
        [In] [MarshalUsing(ConstantElementCount = GammaRampElementCount)] ushort[] ramp);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetDeviceGammaRamp(
        nint deviceContext,
        [Out] [MarshalUsing(ConstantElementCount = GammaRampElementCount)] ushort[] ramp);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DestroyIcon(nint icon);
}
