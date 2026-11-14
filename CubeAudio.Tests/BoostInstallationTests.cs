using CubeAudio.Boost;
using Microsoft.Win32;

namespace CubeAudio.Tests;

public sealed class BoostInstallationTests : IDisposable
{
    private readonly TestRegistry _registry = new();
    private readonly EndpointEffects _effects;
    private readonly string _directory = Directory.CreateTempSubdirectory("CubeAudio.Tests").FullName;
    private readonly byte[] _bundled = [1, 2, 3];
    private readonly string _installed;

    public BoostInstallationTests()
    {
        _effects = new EndpointEffects(_registry.Root);
        _installed = Path.Combine(_directory, "installed.dll");

        File.WriteAllBytes(_installed, _bundled);
    }

    public void Dispose()
    {
        _registry.Dispose();
        Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void GetStatus_FreeEndpoint_IsNotInstalled()
    {
        Assert.Equal(BoostState.NotInstalled, Status(_registry.AddEndpoint()));
    }

    [Fact]
    public void GetStatus_UnknownEndpointOrDriverEffect_IsUnsupported()
    {
        var driver = _registry.AddEndpoint(effects => effects.SetValue(EndpointEffects.EndpointEffect, "{AB3B404A-B18F-4B4F-B91F-77F2DE95EB18}"));

        Assert.Equal(BoostState.Unsupported, Status(Guid.NewGuid().ToString("B")));
        Assert.Equal(BoostState.Unsupported, Status(driver));
    }

    [Fact]
    public void GetStatus_AttachedWithCurrentDllAndSettings_IsActive()
    {
        Assert.Equal(BoostState.Active, Status(Attached()));
    }

    [Fact]
    public void GetStatus_InstalledDllDiffersOrIsMissing_NeedsRepair()
    {
        var endpoint = Attached();

        File.WriteAllBytes(_installed, [9, 9, 9]);
        Assert.Equal(BoostState.NeedsRepair, Status(endpoint));

        File.Delete(_installed);
        Assert.Equal(BoostState.NeedsRepair, Status(endpoint));
    }

    [Fact]
    public void GetStatus_EnhancementsTurnedOff_NeedsRepair()
    {
        var endpoint = Attached();

        using (var effects = _registry.Effects(endpoint))
            effects.SetValue(EndpointEffects.DisableEnhancements, 1, RegistryValueKind.DWord);

        Assert.Equal(BoostState.NeedsRepair, Status(endpoint));
    }

    [Fact]
    public void GetStatus_SettingsMissing_NeedsRepair()
    {
        var endpoint = _registry.AddEndpoint();
        _effects.Attach(endpoint);
        _registry.Root.DeleteSubKeyTree(EffectContract.SettingsKey);

        using (var effects = _registry.Effects(endpoint))
            Assert.Equal(EffectContract.ClassId, effects.GetValue(EndpointEffects.EndpointEffect));

        Assert.Equal(BoostState.NeedsRepair, Status(endpoint));
    }

    private string Attached()
    {
        var endpoint = _registry.AddEndpoint();

        _effects.Attach(endpoint);
        _registry.AddSettings();

        return endpoint;
    }

    private BoostState Status(string endpoint) => BoostInstallation.GetStatus(_registry.Root, endpoint, _bundled, _installed).State;
}
