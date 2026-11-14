using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CubeAudio.Audio;

public sealed class RecordingPlayer : IWaveProvider, IDisposable
{
    private readonly WasapiPlayer _device;
    private readonly float[] _samples;
    private readonly double _step;
    private double _position;

    private RecordingPlayer(MMDevice device, float[] samples, int sampleRate)
    {
        _samples = samples;
        _device = new WasapiPlayerBuilder()
            .WithDevice(device)
            .WithSharedMode()
            .WithEventSync()
            .WithLatency(100)
            .Build();

        var mix = _device.DeviceMixFormat;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(mix.SampleRate, mix.Channels);
        _step = (double)sampleRate / mix.SampleRate;

        _device.PlaybackStopped += (_, _) => Finished?.Invoke();
        _device.Init(this);
    }

    public WaveFormat WaveFormat { get; }

    public event Action? Finished;

    public static RecordingPlayer? TryCreate(float[] samples, int sampleRate)
    {
        using var enumerator = new MMDeviceEnumerator();

        if (!enumerator.TryGetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia, out var device) || device is null)
            return null;

        return new RecordingPlayer(device, samples, sampleRate);
    }

    public void Play() => _device.Play();

    public int Read(Span<byte> buffer)
    {
        var output = MemoryMarshal.Cast<byte, float>(buffer);
        var channels = WaveFormat.Channels;
        var written = 0;

        while (written + channels <= output.Length)
        {
            var index = (int)_position;

            if (index + 1 >= _samples.Length)
                break;

            var fraction = (float)(_position - index);
            var sample = _samples[index] + ((_samples[index + 1] - _samples[index]) * fraction);

            output.Slice(written, channels).Fill(sample);

            written += channels;
            _position += _step;
        }

        return written * sizeof(float);
    }

    public void Dispose() => _device.Dispose();
}
