using CubeAudio.Boost;

namespace CubeAudio.Audio;

public readonly record struct CalibrationResult(bool Heard, double VoiceDb, double GainDb);

public static class Calibration
{
    private const double TargetVoiceDb = -16;
    private const double TargetPeakDb = -3;

    private const double SilenceDb = -60;
    private const double MinimumRangeDb = 6;
    private const double WindowSeconds = 0.05;

    public static CalibrationResult Analyze(float[] samples, int sampleRate)
    {
        var window = (int)(sampleRate * WindowSeconds);
        var levels = new List<double>();

        for (var start = 0; start + window <= samples.Length; start += window)
        {
            var sum = 0.0;

            foreach (var sample in samples.AsSpan(start, window))
                sum += sample * sample;

            levels.Add(ToDb(Math.Sqrt(sum / window)));
        }

        if (levels.Count < 10)
            return default;

        levels.Sort();

        var voice = levels[(int)(levels.Count * 0.9)];
        var room = levels[(int)(levels.Count * 0.1)];

        if (voice < SilenceDb || voice - room < MinimumRangeDb)
            return default;

        var magnitudes = Array.ConvertAll(samples, Math.Abs);
        Array.Sort(magnitudes);

        var peak = ToDb(magnitudes[(int)(magnitudes.Length * 0.999)]);

        var gain = Math.Min(TargetVoiceDb - voice, TargetPeakDb - peak);

        return new CalibrationResult(true, voice, Math.Clamp(Math.Round(gain * 2) / 2, 0, BoostSettings.MaxGainDb));
    }

    public static double ToDb(double amplitude) => 20 * Math.Log10(Math.Max(amplitude, 1e-6));
}
