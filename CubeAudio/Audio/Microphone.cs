using System.Diagnostics;
using NAudio.CoreAudioApi;

namespace CubeAudio.Audio;

public sealed record Microphone(string Id, string Name)
{
    public string Endpoint => Id[Id.LastIndexOf('{')..];

    public static List<Microphone> All()
    {
        using var enumerator = new MMDeviceEnumerator();
        var microphones = new List<Microphone>();

        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            microphones.Add(new Microphone(device.ID, device.FriendlyName));
            device.Dispose();
        }

        return microphones;
    }

    public static string? DefaultId()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();

            if (!enumerator.TryGetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications, out var device) || device is null)
                return null;

            using (device)
                return device.ID;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"No default microphone: {ex.Message}");
            return null;
        }
    }

    public MMDevice Open()
    {
        using var enumerator = new MMDeviceEnumerator();

        return enumerator.GetDevice(Id);
    }
}
