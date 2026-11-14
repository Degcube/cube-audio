using System.Diagnostics;

namespace CubeAudio.Shell;

public static class SystemTool
{
    public static int Run(string name, string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, name), arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;

        process.WaitForExit();

        return process.ExitCode;
    }
}
