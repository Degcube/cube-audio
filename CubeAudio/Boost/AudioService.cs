using CubeAudio.Shell;

namespace CubeAudio.Boost;

public static class AudioService
{
    public static void WhileStopped(Action work)
    {
        SystemTool.Run("net.exe", "stop Audiosrv /y");

        try
        {
            work();
        }
        finally
        {
            SystemTool.Run("net.exe", "start Audiosrv");
        }
    }
}
