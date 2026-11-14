using System.ComponentModel;
using System.Diagnostics;

namespace CubeAudio.Shell;

public static class Elevation
{
    private const int Declined = 1223;

    public static bool Run(params string[] arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, string.Join(' ', arguments))
            {
                UseShellExecute = true,
                Verb = "runas"
            });

            if (process is null)
                return false;

            process.WaitForExit();

            return process.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == Declined)
        {
            return false;
        }
    }
}
