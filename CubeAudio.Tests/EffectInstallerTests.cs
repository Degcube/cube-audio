using CubeAudio.Boost;

namespace CubeAudio.Tests;

public sealed class EffectInstallerTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("CubeAudio.Tests").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Bundled_IsTheEmbeddedEffectDll()
    {
        var bundled = EffectInstaller.Bundled;

        Assert.True(bundled.Length > 1024);
        Assert.Equal("MZ"u8.ToArray(), bundled[..2]);
    }

    [Fact]
    public void IsCurrent_ComparesInstalledFileWithBundledBytes()
    {
        var installed = Path.Combine(_directory, "installed.dll");

        Assert.False(EffectInstaller.IsCurrent([1, 2, 3], installed));

        File.WriteAllBytes(installed, [1, 2, 3]);
        Assert.True(EffectInstaller.IsCurrent([1, 2, 3], installed));

        File.WriteAllBytes(installed, [1, 2, 4]);
        Assert.False(EffectInstaller.IsCurrent([1, 2, 3], installed));
    }
}
