using CubeAudio.Boost;
using Microsoft.Win32;

namespace CubeAudio.Tests;

public sealed class TestRegistry : IDisposable
{
    private const string Parent = @"Software\CubeAudio.Tests";

    private readonly string _path = $@"{Parent}\{Guid.NewGuid():N}";

    public TestRegistry()
    {
        Root = Registry.CurrentUser.CreateSubKey(_path);
    }

    public RegistryKey Root { get; }

    public string AddEndpoint(Action<RegistryKey>? fill = null)
    {
        var endpoint = Guid.NewGuid().ToString("B");

        using var effects = Root.CreateSubKey(EffectsPath(endpoint));
        fill?.Invoke(effects);

        return endpoint;
    }

    public RegistryKey Effects(string endpoint) => Root.OpenSubKey(EffectsPath(endpoint), writable: true)!;

    public RegistryKey? Backup(string endpoint) => Root.OpenSubKey($@"{BoostInstallation.InstallKey}\{endpoint}");

    public void AddSettings() => Root.CreateSubKey(EffectContract.SettingsKey).Dispose();

    public void Dispose()
    {
        Root.Dispose();
        Registry.CurrentUser.DeleteSubKeyTree(_path, throwOnMissingSubKey: false);

        try
        {
            Registry.CurrentUser.DeleteSubKey(Parent, throwOnMissingSubKey: false);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static string EffectsPath(string endpoint) => $@"{EndpointEffects.CaptureKey}\{endpoint}\FxProperties";
}
