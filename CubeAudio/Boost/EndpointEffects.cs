using System.Security.AccessControl;
using Microsoft.Win32;

namespace CubeAudio.Boost;

public enum EndpointState
{
    Missing,
    Free,
    DriverEffect,
    Attached,
    AttachedButDisabled
}

public sealed class EndpointEffects(RegistryKey root)
{
    public const string EndpointEffect = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},7";
    public const string EndpointEffectModes = "{d3993a3f-99c2-4402-b5ec-a92a0367664b},7";
    public const string CompositeEndpointEffect = "{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},15";
    public const string DisableEnhancements = "{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5";
    public const string CaptureKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture";

    private const string DefaultProcessingMode = "{C18E2F7E-933D-4965-B7D1-1EEF228D2AF3}";
    private const string AbsentValues = "Absent";

    private static readonly string[] TouchedValues = [EndpointEffect, EndpointEffectModes, DisableEnhancements];

    public static bool IsEndpoint(string value) => Guid.TryParseExact(value, "B", out _);

    public EndpointState Read(string endpoint)
    {
        using var effects = Open(endpoint, writable: false);

        if (effects is null)
            return EndpointState.Missing;

        if (!IsOurs(effects))
            return HasDriverEffect(effects) ? EndpointState.DriverEffect : EndpointState.Free;

        return effects.GetValue(DisableEnhancements) is int disabled && disabled != 0
            ? EndpointState.AttachedButDisabled
            : EndpointState.Attached;
    }

    public bool IsAttached(string endpoint) => Read(endpoint) is EndpointState.Attached or EndpointState.AttachedButDisabled;

    public List<string> BackedUp()
    {
        using var install = root.OpenSubKey(BoostInstallation.InstallKey);

        return install?.GetSubKeyNames().Where(IsEndpoint).ToList() ?? [];
    }

    public void Attach(string endpoint)
    {
        using var effects = Open(endpoint, writable: true)
            ?? throw new InvalidOperationException("Windows keeps no effect settings for this microphone.");

        if (HasDriverEffect(effects))
            throw new InvalidOperationException("This microphone already has an endpoint effect from its driver, CubeAudio can't share that slot.");

        BackUp(endpoint, effects);

        effects.SetValue(EndpointEffect, EffectContract.ClassId, RegistryValueKind.String);
        effects.SetValue(EndpointEffectModes, new[] { DefaultProcessingMode }, RegistryValueKind.MultiString);
        effects.SetValue(DisableEnhancements, 0, RegistryValueKind.DWord);
    }

    public void Detach(string endpoint)
    {
        using var install = root.OpenSubKey(BoostInstallation.InstallKey, writable: true);
        using var backup = install?.OpenSubKey(endpoint);
        using var effects = Open(endpoint, writable: true);

        if (effects is not null && IsOurs(effects))
        {
            if (backup is null)
            {
                effects.DeleteValue(EndpointEffect, throwOnMissingValue: false);
                effects.DeleteValue(EndpointEffectModes, throwOnMissingValue: false);
            }
            else
            {
                foreach (var name in TouchedValues)
                {
                    if (backup.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is { } original)
                        effects.SetValue(name, original, backup.GetValueKind(name));
                    else
                        effects.DeleteValue(name, throwOnMissingValue: false);
                }
            }
        }

        install?.DeleteSubKeyTree(endpoint, throwOnMissingSubKey: false);
    }

    private void BackUp(string endpoint, RegistryKey effects)
    {
        using var install = root.CreateSubKey(BoostInstallation.InstallKey);

        using (var existing = install.OpenSubKey(endpoint))
        {
            if (existing is not null)
                return;
        }

        using var backup = install.CreateSubKey(endpoint);
        var absent = new List<string>();

        foreach (var name in TouchedValues)
        {
            if (effects.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is { } value)
                backup.SetValue(name, value, effects.GetValueKind(name));
            else
                absent.Add(name);
        }

        backup.SetValue(AbsentValues, absent.ToArray(), RegistryValueKind.MultiString);
    }

    private RegistryKey? Open(string endpoint, bool writable)
    {
        var path = $@"{CaptureKey}\{endpoint}\FxProperties";

        return writable
            ? root.OpenSubKey(path, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.SetValue | RegistryRights.QueryValues)
            : root.OpenSubKey(path);
    }

    private static bool IsOurs(RegistryKey effects)
    {
        return string.Equals(effects.GetValue(EndpointEffect) as string, EffectContract.ClassId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasDriverEffect(RegistryKey effects)
    {
        if (effects.GetValue(CompositeEndpointEffect) is string[] { Length: > 0 })
            return true;

        return effects.GetValue(EndpointEffect) is string single && Guid.TryParse(single, out var id) && id != Guid.Empty && !IsOurs(effects);
    }
}
