using CubeAudio.Audio;
using CubeAudio.Boost;

namespace CubeAudio.Tests;

public class CalibrationTests
{
    private const int SampleRate = 96000;

    [Fact]
    public void Analyze_QuietVoice_BringsItToTheTargetLevel()
    {
        var result = Calibration.Analyze(Speech(0.02f, 0.0003f), SampleRate);

        Assert.True(result.Heard);
        Assert.Equal(-37, result.VoiceDb, 0.5);
        Assert.Equal(21, result.GainDb);
    }

    [Fact]
    public void Analyze_VoiceThatIsLoudEnough_NeedsNoGain()
    {
        var result = Calibration.Analyze(Speech(0.25f, 0.0003f), SampleRate);

        Assert.True(result.Heard);
        Assert.Equal(0, result.GainDb);
    }

    [Fact]
    public void Analyze_VeryQuietVoice_IsCappedAtTheMaximum()
    {
        var result = Calibration.Analyze(Speech(0.002f, 0.0001f), SampleRate);

        Assert.True(result.Heard);
        Assert.Equal(BoostSettings.MaxGainDb, result.GainDb);
    }

    [Fact]
    public void Analyze_NoiseOrSilence_IsNotHeard()
    {
        Assert.False(Calibration.Analyze(Speech(0, 0.0005f), SampleRate).Heard);
        Assert.False(Calibration.Analyze(new float[SampleRate * 5], SampleRate).Heard);
    }

    [Fact]
    public void Analyze_SingleClick_DoesNotChangeTheResult()
    {
        var samples = Speech(0.02f, 0.0003f);
        var expected = Calibration.Analyze(samples, SampleRate).GainDb;

        samples[1000] = 0.9f;

        Assert.Equal(expected, Calibration.Analyze(samples, SampleRate).GainDb);
    }

    [Fact]
    public void Analyze_RecordingTooShort_IsNotHeard()
    {
        Assert.False(Calibration.Analyze(Speech(0.02f, 0.0003f)[..(SampleRate / 5)], SampleRate).Heard);
    }

    private static float[] Speech(float amplitude, float noise)
    {
        var random = new Random(1);
        var samples = new float[SampleRate * 5];

        for (var i = 0; i < samples.Length; i++)
        {
            var talking = i % (SampleRate / 2) < SampleRate * 3 / 10;
            var voice = talking ? amplitude * MathF.Sin(MathF.Tau * 180 * i / SampleRate) : 0;

            samples[i] = voice + (noise * (((float)random.NextDouble() * 2) - 1));
        }

        return samples;
    }
}
