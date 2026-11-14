using System.Security.AccessControl;
using Microsoft.Win32;

namespace CubeAudio.Boost;

public sealed class BoostSettings(RegistryKey root)
{
    public const double MaxGainDb = EffectContract.MaxGainTenthsOfDb / 10.0;
    public const double DefaultGainDb = 12;

    public bool Enabled
    {
        get => Read(EffectContract.EnabledValue, 1) != 0;
        set => Write(EffectContract.EnabledValue, value ? 1 : 0);
    }

    public double GainDb
    {
        get => Math.Clamp(Read(EffectContract.GainValue, (int)(DefaultGainDb * 10)) / 10.0, 0, MaxGainDb);
        set => Write(EffectContract.GainValue, (int)Math.Round(Math.Clamp(value, 0, MaxGainDb) * 10));
    }

    private int Read(string name, int fallback)
    {
        using var key = root.OpenSubKey(EffectContract.SettingsKey);

        return key?.GetValue(name) is int value ? value : fallback;
    }

    private void Write(string name, int value)
    {
        using var key = root.OpenSubKey(EffectContract.SettingsKey, RegistryKeyPermissionCheck.ReadWriteSubTree, RegistryRights.SetValue | RegistryRights.QueryValues)
            ?? throw new InvalidOperationException("The boost settings are missing, click Repair.");

        key.SetValue(name, value, RegistryValueKind.DWord);
    }
}
