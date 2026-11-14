using System.Security;
using CubeAudio.Shell;
using Microsoft.Win32;

namespace CubeAudio.Boost;

public enum BoostState
{
    NotInstalled,
    Active,
    NeedsRepair,
    Unsupported
}

public readonly record struct BoostStatus(BoostState State, string Text);

public static class BoostInstallation
{
    public const string InstallKey = EffectContract.SettingsKey + @"\Install";

    public static string InstalledDll => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "CubeAudio", "CubeAudioApo.dll");
    public static bool EffectIsRunning => NativeMethods.EventExists(EffectContract.RunningEvent);

    public static BoostStatus GetStatus(string endpoint) => GetStatus(Registry.LocalMachine, endpoint, EffectInstaller.Bundled, InstalledDll);

    public static BoostStatus GetStatus(RegistryKey root, string endpoint, byte[] bundledDll, string installedDll)
    {
        try
        {
            switch (new EndpointEffects(root).Read(endpoint))
            {
                case EndpointState.Missing:
                    return new BoostStatus(BoostState.Unsupported, "Windows keeps no effect settings for this microphone");

                case EndpointState.DriverEffect:
                    return new BoostStatus(BoostState.Unsupported, "This microphone already has an endpoint effect from its driver");

                case EndpointState.Free:
                    return new BoostStatus(BoostState.NotInstalled, "Boost is not installed on this microphone");

                case EndpointState.AttachedButDisabled:
                    return new BoostStatus(BoostState.NeedsRepair, "Audio enhancements are turned off for this microphone");
            }

            if (!EffectInstaller.IsCurrent(bundledDll, installedDll))
                return new BoostStatus(BoostState.NeedsRepair, "The boost effect needs an update");

            using var settings = root.OpenSubKey(EffectContract.SettingsKey);

            return settings is null
                ? new BoostStatus(BoostState.NeedsRepair, "The boost settings are missing")
                : new BoostStatus(BoostState.Active, "Boost is active on this microphone");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            return new BoostStatus(BoostState.Unsupported, ex.Message);
        }
    }
}
