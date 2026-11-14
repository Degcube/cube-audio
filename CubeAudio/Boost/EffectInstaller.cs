using System.Security.AccessControl;
using System.Security.Principal;
using CubeAudio.Shell;
using Microsoft.Win32;

namespace CubeAudio.Boost;

public static class EffectInstaller
{
    public static bool IsInstalled
    {
        get
        {
            using var settings = Registry.LocalMachine.OpenSubKey(EffectContract.SettingsKey);

            return settings is not null || File.Exists(BoostInstallation.InstalledDll);
        }
    }

    private const string Resource = "CubeAudioApo.dll";

    private static readonly Lazy<byte[]> Embedded = new(ReadEmbedded);

    public static byte[] Bundled => Embedded.Value;

    public static bool IsCurrent(ReadOnlySpan<byte> bundled, string installed)
    {
        return File.Exists(installed) && File.ReadAllBytes(installed).AsSpan().SequenceEqual(bundled);
    }

    public static void Install()
    {
        var installed = BoostInstallation.InstalledDll;

        Directory.CreateDirectory(Path.GetDirectoryName(installed)!);

        if (!IsCurrent(Bundled, installed))
            WhenReleased(() => File.WriteAllBytes(installed, Bundled));

        var code = SystemTool.Run("regsvr32.exe", $"/s \"{installed}\"");

        if (code != 0)
            throw new InvalidOperationException($"Registering the boost effect failed (regsvr32 exit code {code}).");

        using var settings = Registry.LocalMachine.CreateSubKey(EffectContract.SettingsKey);
        var security = settings.GetAccessControl();
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        security.AddAccessRule(new RegistryAccessRule(users, RegistryRights.SetValue | RegistryRights.ReadKey, InheritanceFlags.None, PropagationFlags.None, AccessControlType.Allow));
        settings.SetAccessControl(security);
    }

    public static void Remove()
    {
        var installed = BoostInstallation.InstalledDll;

        if (File.Exists(installed))
        {
            _ = SystemTool.Run("regsvr32.exe", $"/u /s \"{installed}\"");
            WhenReleased(() => Directory.Delete(Path.GetDirectoryName(installed)!, recursive: true));
        }

        Registry.LocalMachine.DeleteSubKeyTree(EffectContract.SettingsKey, throwOnMissingSubKey: false);
    }

    private static byte[] ReadEmbedded()
    {
        using var resource = typeof(EffectInstaller).Assembly.GetManifestResourceStream(Resource)
            ?? throw new InvalidOperationException($"{Resource} is not embedded in this build.");
        using var buffer = new MemoryStream();

        resource.CopyTo(buffer);

        return buffer.ToArray();
    }

    private static void WhenReleased(Action fileOperation)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                fileOperation();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < 40)
            {
                Thread.Sleep(250);
            }
        }
    }
}
