using System.Runtime.InteropServices;

namespace CubeAudio.Shell;

internal static partial class NativeMethods
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCaptionColor = 35;
    private const uint Synchronize = 0x00100000;
    private const int AccessDenied = 5;

    public static void UseDarkTitleBar(IntPtr window, byte red, byte green, byte blue)
    {
        var enabled = 1;
        var color = red | (green << 8) | (blue << 16);

        _ = DwmSetWindowAttribute(window, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        _ = DwmSetWindowAttribute(window, DwmCaptionColor, ref color, sizeof(int));
    }

    public static bool EventExists(string name)
    {
        var handle = OpenEventW(Synchronize, false, name);

        if (handle == IntPtr.Zero)
            return Marshal.GetLastPInvokeError() == AccessDenied;

        _ = CloseHandle(handle);
        return true;
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenEventW(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, string name);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr handle);
}
