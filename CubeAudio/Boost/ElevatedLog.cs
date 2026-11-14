using System.Diagnostics;

namespace CubeAudio.Boost;

public static class ElevatedLog
{
    private const long MaxLength = 256 * 1024;

    public static string LogFile { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "CubeAudio", "CubeAudio.log");

    public static void Delete()
    {
        var directory = Path.GetDirectoryName(LogFile)!;

        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    public static void Write(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogFile)!);

            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxLength)
                File.Delete(LogFile);

            File.AppendAllText(LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"Log unavailable: {ex.Message}");
        }
    }
}
