using CubeAudio.Boost;

namespace CubeAudio.Tests;

public sealed class BoostSettingsTests : IDisposable
{
    private readonly TestRegistry _registry = new();
    private readonly BoostSettings _settings;

    public BoostSettingsTests()
    {
        _settings = new BoostSettings(_registry.Root);
    }

    public void Dispose() => _registry.Dispose();

    [Fact]
    public void Defaults_WithoutSettingsKey_AreEnabledAtDefaultGain()
    {
        Assert.True(_settings.Enabled);
        Assert.Equal(BoostSettings.DefaultGainDb, _settings.GainDb);
    }

    [Fact]
    public void Values_RoundTrip()
    {
        _registry.AddSettings();

        _settings.GainDb = 21.5;
        _settings.Enabled = false;

        var reread = new BoostSettings(_registry.Root);

        Assert.Equal(21.5, reread.GainDb);
        Assert.False(reread.Enabled);
    }

    [Theory]
    [InlineData(45, BoostSettings.MaxGainDb)]
    [InlineData(-3, 0)]
    [InlineData(12.34, 12.3)]
    public void GainDb_IsClampedAndStoredInTenths(double written, double expected)
    {
        _registry.AddSettings();

        _settings.GainDb = written;

        Assert.Equal(expected, _settings.GainDb);
    }

    [Fact]
    public void Write_WithoutSettingsKey_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _settings.GainDb = 10);
        Assert.Throws<InvalidOperationException>(() => _settings.Enabled = false);
    }
}
