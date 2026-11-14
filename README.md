# CubeAudio

Makes a microphone that is too quiet louder (C# / .NET 10 / WPF, plus a small C++ DLL). Built for the HyperX Cloud III, whose mic is still too quiet at 100 % in Windows. Keeping it simple with only one slider, one level meter, no virtual audio device and no program that has to run in the background.

Meant for microphones that have no boost of their own, typically USB headsets where 100 % in Windows is the maximum. If your device offers "Microphone Boost" in its Windows level settings, use that first.

## Installing

Download `CubeAudio-<version>-win-x64.zip` from the [latest release](https://github.com/Degcube/cube-audio/releases/latest), unpack it anywhere and start `CubeAudio.exe`. It needs the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64). If it is missing, CubeAudio says so on start and offers the download. To get rid of it again, click **Remove** in the app and delete the file.

### Building from source

```powershell
.\install.ps1
```

Builds to `%LOCALAPPDATA%\Programs\CubeAudio` and creates a Start menu shortcut. Needs Visual Studio with the C++ workload (for the DLL) and the .NET 10 SDK. Only tested on Windows 11 (build 26200).

```powershell
.\uninstall.ps1
```

Removes everything again: it takes the boost off all microphones (this asks for admin rights and restarts Windows audio for a moment) and then deletes the app, the shortcut and the log. Nothing listed under [What Enable changes in Windows](#what-enable-changes-in-windows) is left behind.

### Signing

`install.ps1` signs `CubeAudioApo.dll` and `CubeAudio.exe` through `sign.ps1` as soon as a `signing.json` sits next to the script. It holds the certificate thumbprint and the timestamp server, the template is `signing.example.json`. The file is in `.gitignore`: my builds are signed, but my certificate is of course not part of the repo.

The installed app is a single file: `CubeAudio.exe` contains the program, its libraries and the effect DLL. Enable runs with admin rights, so there are deliberately no loose files next to the exe that could be swapped beforehand. With a signed build, any change to that one file breaks its signature, and the admin prompt no longer shows the publisher.

Builds from Visual Studio or `dotnet build` are never signed.

### Without a signature

If `signing.json` is missing, `sign.ps1` skips the step and `install.ps1` installs unsigned. CubeAudio works the same:

- Windows loads the unsigned effect DLL into the audio engine just the same (tested on Windows 11, build 26200).
- Enable writes exactly the same things as with a signed build, see [What Enable changes in Windows](#what-enable-changes-in-windows). There is no additional registry entry and no change to security settings for the unsigned case.

Two things do differ. The admin prompt on Enable shows "Unknown publisher" instead of a publisher name. And the tamper protection described above is gone: an unsigned exe looks the same whether or not it was modified.

## Usage

1. Pick the microphone at the top right. Preselected is the one that already has the boost, otherwise the default mic.
2. **Enable** attaches the boost to this microphone. This asks for admin rights once and restarts Windows audio for a moment, so running calls and recordings get interrupted.
3. **Auto-calibrate**: talk normally for 5 seconds and CubeAudio sets the gain. Or drag the slider by hand until your voice lands in the brighter part of the level meter.
4. **Test recording** records 5 seconds and plays them back: this is how others hear you.

After that the window can be closed. The setting stays, also after a reboot. The power button next to the dB value switches the boost off and on for comparison, without admin rights. **Remove** takes it off the microphone again and restores the previous state.

The level meter shows the signal the way it arrives at Discord and the like, so after the boost. When it turns red, the limiter is working: the gain is too high for the current volume, but nothing distorts.

## How it works

Windows can load effects per audio device (Audio Processing Objects, the technology behind "audio enhancements"). `CubeAudioApo.dll` is such an effect: gain plus a limiter, nothing else. It sits as an endpoint effect directly on the microphone input, so every app gets the louder signal and there is no second device in the sound settings.

- The effect runs in the Windows audio engine (`audiodg.exe`), not in CubeAudio. The app is only the settings window.
- Gain and on / off are stored in `HKLM\SOFTWARE\CubeAudio`. The effect picks up changes immediately, so the slider works live.
- The limiter turns down on the very sample that would be too loud and comes back up over 150 ms. Nothing above -1 dBFS ever comes out, and there is no added latency.

## What Enable changes in Windows

Signed or unsigned makes no difference here. `{CubeAudio}` stands for the class ID `{31B5EE6D-6025-41F0-BDCD-1FA50BC6DB9F}`, `{Microphone}` for the ID of the selected device.

| Where | What |
|---|---|
| `C:\Program Files\CubeAudio\CubeAudioApo.dll` | The effect DLL. The audio engine runs as a service account and cannot load anything from the user profile. |
| `HKLM\SOFTWARE\Classes\CLSID\{CubeAudio}` | COM registration of the DLL (`regsvr32`). |
| `HKLM\SOFTWARE\Classes\AudioEngine\AudioProcessingObjects\{CubeAudio}` | Registration as an audio effect with the audio engine. |
| `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture\{Microphone}\FxProperties` | Three values on the microphone, see below. |
| `HKLM\SOFTWARE\CubeAudio` | `Enabled` and `GainTenthsOfDb`. Regular users may write these two values, which is why the slider needs no admin rights. |
| `HKLM\SOFTWARE\CubeAudio\Install\{Microphone}` | Backup of the three values on the microphone as they were before. |
| `C:\ProgramData\CubeAudio\CubeAudio.log` | Log of Enable, Repair and Remove. |

The three values under `FxProperties`:

| Value | Content | Meaning |
|---|---|---|
| `{d04e05a6-594b-4fb6-a80d-01af5eed7d1d},7` | `{CubeAudio}` | Endpoint effect of the microphone |
| `{d3993a3f-99c2-4402-b5ec-a92a0367664b},7` | `{C18E2F7E-933D-4965-B7D1-1EEF228D2AF3}` | The effect applies in the default processing mode |
| `{1da5d803-d492-4edd-8c23-e0c0ffee7f0e},5` | `0` | "Audio enhancements" on, otherwise Windows loads no effects |

Enable then checks whether Windows really starts the effect: it records from the microphone for a moment and looks whether the effect reports itself as running. An effect that Windows cannot start would otherwise take down the whole microphone. If it doesn't come up, everything is rolled back and the microphone is as it was.

CubeAudio touches nothing else, in particular no Windows security settings.

**Remove** restores the three values on the microphone from the backup. With the last microphone the DLL, both registrations and `HKLM\SOFTWARE\CubeAudio` disappear as well. Only the log stays, `uninstall.ps1` is what cleans that up.

## When it stops working

A driver update or a different USB port can reset the entry on the microphone, because Windows then creates the device anew. Open CubeAudio: if the button at the bottom says **Enable** or **Repair**, click it once.

The status line doesn't rely on the registry alone. While the window is open, CubeAudio listens on the microphone and asks the effect whether it is running. If it isn't, the status reads "Windows is not running the boost effect" and offers **Repair**.

Enable, Repair and Remove log what they do: `C:\ProgramData\CubeAudio\CubeAudio.log`. If something fails, the reason is in there.

## Development

`CubeAudio.slnx` contains four projects: the app (`CubeAudio`), its tests (`CubeAudio.Tests`, xUnit), the effect DLL (`CubeAudio.Apo`, C++) and its test program (`CubeAudio.Apo.Test`, C++). The two C++ projects have a single configuration (Release, x64), so Debug and Release of the solution build the same DLL.

- `dotnet test CubeAudio.Tests` checks the registry logic (attaching, backup, restoring), the status display, the settings and the calibration. The registry tests run without admin rights against a throwaway branch under `HKCU\Software\CubeAudio.Tests`.
- In Visual Studio the solution builds the DLL first, then the app. With `CubeAudio.Apo.Test` as the startup project, F5 tests the freshly built DLL.
- `CubeAudio.Apo\build.ps1 -Test` does the same in a terminal: build the DLL, then check gain, limiter and the behaviour towards the audio engine, without registering anything.
- `dotnet build CubeAudio\CubeAudio.csproj` (and with it `install.ps1`) builds the DLL when needed through Visual Studio's MSBuild. `dotnet build` on the whole solution does not work, the dotnet CLI cannot build C++. When `dotnet build` or `dotnet test` has to build the DLL itself, it calls `pwsh`, so that path needs PowerShell 7.
- What the app and the DLL need to know about each other (class ID, registry path, value names, event name, maximum gain) is written down exactly once on each side: `CubeAudio.Apo\Contract.h` and `CubeAudio\Boost\EffectContract.cs`. A test compares the two, so a change made on just one side fails the next `dotnet test`.
- The version lives in `Directory.Build.props` and applies to the app and the DLL.
- `release.ps1` builds the release package into `artifacts\`: the signed exe with `LICENSE` and `THIRD-PARTY-NOTICES.txt` as a zip. It refuses to run with uncommitted changes or without a valid signature. `install.ps1` and `release.ps1` share the build and signing step in `publish.ps1`.
- The DLL is built reproducibly (`/Brepro`): same source, same file. CubeAudio compares the installed DLL with the one embedded in the exe and only shows **Repair** when something really changed.
- Microphones whose driver already brings its own endpoint effect (many Realtek onboard inputs) are refused by CubeAudio rather than having the driver effect replaced.

## License

[MIT](LICENSE)
