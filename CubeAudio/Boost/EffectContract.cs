namespace CubeAudio.Boost;

public static class EffectContract
{
    public const string ClassId = "{31B5EE6D-6025-41F0-BDCD-1FA50BC6DB9F}";
    public const string SettingsKey = @"SOFTWARE\CubeAudio";
    public const string EnabledValue = "Enabled";
    public const string GainValue = "GainTenthsOfDb";
    public const string RunningEvent = @"Global\CubeAudioApoRunning";
    public const int MaxGainTenthsOfDb = 300;
}
