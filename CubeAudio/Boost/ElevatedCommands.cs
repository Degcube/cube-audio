using System.Diagnostics;
using System.Windows;
using CubeAudio.Audio;
using Microsoft.Win32;

namespace CubeAudio.Boost;

public static class ElevatedCommands
{
    public const string Attach = "--attach";
    public const string Detach = "--detach";
    public const string DetachAll = "--detach-all";

    private const int RecordingAttempts = 3;

    public static bool IsCommand(string[] arguments)
    {
        return arguments.Length > 0 && arguments[0] is Attach or Detach or DetachAll;
    }

    public static int Run(string[] arguments)
    {
        ElevatedLog.Write(string.Join(' ', arguments));

        try
        {
            var effects = new EndpointEffects(Registry.LocalMachine);

            switch (arguments)
            {
                case [Attach, var endpoint] when EndpointEffects.IsEndpoint(endpoint):
                    AttachAndVerify(effects, endpoint);
                    break;

                case [Detach, var endpoint] when EndpointEffects.IsEndpoint(endpoint):
                    DetachAndCleanUp(effects, [endpoint]);
                    break;

                case [DetachAll]:
                    if (EffectInstaller.IsInstalled)
                        DetachAndCleanUp(effects, effects.BackedUp());

                    ElevatedLog.Delete();
                    return 0;

                default:
                    throw new ArgumentException("Unknown command.");
            }

            ElevatedLog.Write("done");
            return 0;
        }
        catch (Exception ex)
        {
            ElevatedLog.Write($"failed: {ex}");
            MessageBox.Show($"{ex.Message}\n\nDetails: {ElevatedLog.LogFile}", "CubeAudio", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    private static void AttachAndVerify(EndpointEffects effects, string endpoint)
    {
        var attached = false;

        try
        {
            AudioService.WhileStopped(() =>
            {
                effects.Attach(endpoint);
                attached = true;
                EffectInstaller.Install();
            });

            if (!RecordsThroughEffect(endpoint))
                throw new InvalidOperationException("Windows doesn't start the boost effect on this microphone.");
        }
        catch (Exception ex) when (attached)
        {
            ElevatedLog.Write($"rolling back: {ex.Message}");
            DetachAndCleanUp(effects, [endpoint]);

            throw new InvalidOperationException($"{ex.Message} Everything was put back, the microphone works as before.", ex);
        }
    }

    private static void DetachAndCleanUp(EndpointEffects effects, List<string> endpoints)
    {
        AudioService.WhileStopped(() =>
        {
            endpoints.ForEach(effects.Detach);

            if (effects.BackedUp().Count == 0)
                EffectInstaller.Remove();
        });
    }

    private static bool RecordsThroughEffect(string endpoint)
    {
        var id = $"{{0.0.1.00000000}}.{endpoint}";
        var appeared = Stopwatch.StartNew();

        while (Microphone.All().All(microphone => microphone.Id != id))
        {
            if (appeared.Elapsed > TimeSpan.FromSeconds(15))
            {
                ElevatedLog.Write("microphone did not come back");
                return false;
            }

            Thread.Sleep(250);
        }

        for (var attempt = 1; attempt <= RecordingAttempts; attempt++)
        {
            Thread.Sleep(500);

            try
            {
                using var monitor = new MicrophoneMonitor(new Microphone(id, ""));

                Thread.Sleep(700);

                var running = BoostInstallation.EffectIsRunning;
                ElevatedLog.Write($"attempt {attempt}: recording={!monitor.Stopped} effect running={running}");

                if (!monitor.Stopped && running)
                    return true;
            }
            catch (Exception ex)
            {
                ElevatedLog.Write($"attempt {attempt}: recording failed: {ex.Message}");
            }
        }

        return false;
    }
}
