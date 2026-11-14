using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CubeAudio.Audio;

public sealed class MicrophoneMonitor : IDisposable
{
    private readonly MMDevice _device;
    private readonly WasapiRecorder _capture;
    private readonly int _channels;
    private readonly Lock _lock = new();
    private float _peak;
    private float[]? _recording;
    private int _recorded;
    private TaskCompletionSource<float[]>? _recordingDone;

    public MicrophoneMonitor(Microphone microphone)
    {
        _device = microphone.Open();

        try
        {
            _capture = new WasapiRecorderBuilder()
                .WithDevice(_device)
                .WithSharedMode()
                .WithEventSync()
                .WithBufferLength(50)
                .Build();

            if (_capture.WaveFormat.BitsPerSample != 32)
                throw new NotSupportedException($"Unexpected microphone format: {_capture.WaveFormat}");

            SampleRate = _capture.WaveFormat.SampleRate;
            _channels = _capture.WaveFormat.Channels;

            _capture.DataAvailable += OnData;
            _capture.RecordingStopped += OnStopped;
            _capture.StartRecording();
        }
        catch
        {
            _capture?.Dispose();
            _device.Dispose();
            throw;
        }
    }

    public int SampleRate { get; }

    public bool Stopped { get; private set; }

    public float TakePeak()
    {
        lock (_lock)
        {
            var peak = _peak;
            _peak = 0;

            return peak;
        }
    }

    public Task<float[]> Record(TimeSpan duration)
    {
        lock (_lock)
        {
            if (Stopped)
                return Task.FromException<float[]>(new InvalidOperationException("The microphone is not available."));

            _recordingDone?.TrySetCanceled();
            _recording = new float[(int)(duration.TotalSeconds * SampleRate)];
            _recorded = 0;
            _recordingDone = new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            return _recordingDone.Task;
        }
    }

    private void OnData(ReadOnlySpan<byte> buffer, AudioClientBufferFlags flags, long devicePosition, long qpcPosition)
    {
        var samples = MemoryMarshal.Cast<byte, float>(buffer);

        var silent = flags.HasFlag(AudioClientBufferFlags.Silent);

        lock (_lock)
        {
            for (var offset = 0; offset + _channels <= samples.Length; offset += _channels)
            {
                var sum = 0f;

                foreach (var sample in samples.Slice(offset, _channels))
                {
                    if (silent)
                        break;

                    _peak = Math.Max(_peak, Math.Abs(sample));
                    sum += sample;
                }

                if (_recording is not null && _recorded < _recording.Length)
                    _recording[_recorded++] = sum / _channels;
            }

            if (_recording is not null && _recorded == _recording.Length)
            {
                _recordingDone!.TrySetResult(_recording);
                _recording = null;
                _recordingDone = null;
            }
        }
    }

    private void OnStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            Stopped = true;
            _recording = null;
            _recordingDone?.TrySetException(e.Exception ?? new InvalidOperationException("The microphone stopped."));
            _recordingDone = null;
        }
    }

    public void Dispose()
    {
        _capture.DataAvailable -= OnData;
        _capture.Dispose();
        _device.Dispose();
    }
}
