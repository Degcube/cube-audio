using CubeAudio.Boost;
using Microsoft.Win32;

namespace CubeAudio.Tests;

public sealed class EndpointEffectsTests : IDisposable
{
    private const string DriverEffect = "{AB3B404A-B18F-4B4F-B91F-77F2DE95EB18}";
    private const string Unrelated = "{9e6136e0-57ab-4949-b57a-3627be142855},100";

    private readonly TestRegistry _registry = new();
    private readonly EndpointEffects _effects;

    public EndpointEffectsTests()
    {
        _effects = new EndpointEffects(_registry.Root);
    }

    public void Dispose() => _registry.Dispose();

    [Fact]
    public void Read_UnknownEndpoint_IsMissing()
    {
        Assert.Equal(EndpointState.Missing, _effects.Read(Guid.NewGuid().ToString("B")));
    }

    [Fact]
    public void Read_EmptyEndpoint_IsFree()
    {
        Assert.Equal(EndpointState.Free, _effects.Read(_registry.AddEndpoint()));
    }

    [Fact]
    public void Read_EndpointWithDriverEffect_IsDriverEffect()
    {
        var single = _registry.AddEndpoint(effects => effects.SetValue(EndpointEffects.EndpointEffect, DriverEffect));
        var composite = _registry.AddEndpoint(effects => effects.SetValue(EndpointEffects.CompositeEndpointEffect, new[] { DriverEffect }, RegistryValueKind.MultiString));

        Assert.Equal(EndpointState.DriverEffect, _effects.Read(single));
        Assert.Equal(EndpointState.DriverEffect, _effects.Read(composite));
    }

    [Fact]
    public void Read_EndpointWithEmptyEffectSlot_IsFree()
    {
        var endpoint = _registry.AddEndpoint(effects => effects.SetValue(EndpointEffects.EndpointEffect, Guid.Empty.ToString("B")));

        Assert.Equal(EndpointState.Free, _effects.Read(endpoint));
    }

    [Fact]
    public void Attach_WritesEffectAndEnablesEnhancements()
    {
        var endpoint = _registry.AddEndpoint(effects => effects.SetValue(EndpointEffects.DisableEnhancements, 1, RegistryValueKind.DWord));

        _effects.Attach(endpoint);

        using var effects = _registry.Effects(endpoint);

        Assert.Equal(EffectContract.ClassId, effects.GetValue(EndpointEffects.EndpointEffect));
        Assert.Single((string[])effects.GetValue(EndpointEffects.EndpointEffectModes)!);
        Assert.Equal(0, effects.GetValue(EndpointEffects.DisableEnhancements));
        Assert.Equal(EndpointState.Attached, _effects.Read(endpoint));
        Assert.True(_effects.IsAttached(endpoint));
    }

    [Fact]
    public void Attach_EndpointWithDriverEffect_ThrowsAndChangesNothing()
    {
        var endpoint = _registry.AddEndpoint(effects => effects.SetValue(EndpointEffects.EndpointEffect, DriverEffect));

        Assert.Throws<InvalidOperationException>(() => _effects.Attach(endpoint));

        using var effects = _registry.Effects(endpoint);

        Assert.Equal(DriverEffect, effects.GetValue(EndpointEffects.EndpointEffect));
        Assert.Null(_registry.Backup(endpoint));
    }

    [Fact]
    public void Attach_UnknownEndpoint_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => _effects.Attach(Guid.NewGuid().ToString("B")));
    }

    [Fact]
    public void Detach_RestoresValuesFromBeforeAttach()
    {
        var endpoint = _registry.AddEndpoint(effects =>
        {
            effects.SetValue(EndpointEffects.DisableEnhancements, 1, RegistryValueKind.DWord);
            effects.SetValue(Unrelated, "untouched");
        });

        _effects.Attach(endpoint);
        _effects.Detach(endpoint);

        using var effects = _registry.Effects(endpoint);

        Assert.Equal(1, effects.GetValue(EndpointEffects.DisableEnhancements));
        Assert.Equal(RegistryValueKind.DWord, effects.GetValueKind(EndpointEffects.DisableEnhancements));
        Assert.Null(effects.GetValue(EndpointEffects.EndpointEffect));
        Assert.Null(effects.GetValue(EndpointEffects.EndpointEffectModes));
        Assert.Equal("untouched", effects.GetValue(Unrelated));
        Assert.Null(_registry.Backup(endpoint));
        Assert.Equal(EndpointState.Free, _effects.Read(endpoint));
    }

    [Fact]
    public void Detach_RemovesValuesThatDidNotExistBefore()
    {
        var endpoint = _registry.AddEndpoint();

        _effects.Attach(endpoint);
        _effects.Detach(endpoint);

        using var effects = _registry.Effects(endpoint);

        Assert.Empty(effects.GetValueNames());
    }

    [Fact]
    public void Attach_Twice_KeepsTheOriginalBackup()
    {
        var endpoint = _registry.AddEndpoint(effects => effects.SetValue(EndpointEffects.DisableEnhancements, 1, RegistryValueKind.DWord));

        _effects.Attach(endpoint);
        _effects.Attach(endpoint);
        _effects.Detach(endpoint);

        using var effects = _registry.Effects(endpoint);

        Assert.Equal(1, effects.GetValue(EndpointEffects.DisableEnhancements));
        Assert.Null(effects.GetValue(EndpointEffects.EndpointEffect));
    }

    [Fact]
    public void Detach_EndpointResetByDriver_LeavesItAloneAndDropsBackup()
    {
        var endpoint = _registry.AddEndpoint();

        _effects.Attach(endpoint);

        using (var reset = _registry.Effects(endpoint))
        {
            reset.SetValue(EndpointEffects.EndpointEffect, DriverEffect);
            reset.SetValue(EndpointEffects.DisableEnhancements, 1, RegistryValueKind.DWord);
        }

        _effects.Detach(endpoint);

        using var effects = _registry.Effects(endpoint);

        Assert.Equal(DriverEffect, effects.GetValue(EndpointEffects.EndpointEffect));
        Assert.Equal(1, effects.GetValue(EndpointEffects.DisableEnhancements));
        Assert.Null(_registry.Backup(endpoint));
    }

    [Fact]
    public void Detach_WithoutBackup_OnlyTakesTheEffectOut()
    {
        var endpoint = _registry.AddEndpoint();

        _effects.Attach(endpoint);
        _registry.Root.DeleteSubKeyTree($@"{BoostInstallation.InstallKey}\{endpoint}");
        _effects.Detach(endpoint);

        using var effects = _registry.Effects(endpoint);

        Assert.Null(effects.GetValue(EndpointEffects.EndpointEffect));
        Assert.Null(effects.GetValue(EndpointEffects.EndpointEffectModes));
        Assert.Equal(0, effects.GetValue(EndpointEffects.DisableEnhancements));
    }

    [Fact]
    public void Read_AttachedWithEnhancementsTurnedOff_IsAttachedButDisabled()
    {
        var endpoint = _registry.AddEndpoint();

        _effects.Attach(endpoint);

        using (var effects = _registry.Effects(endpoint))
            effects.SetValue(EndpointEffects.DisableEnhancements, 1, RegistryValueKind.DWord);

        Assert.Equal(EndpointState.AttachedButDisabled, _effects.Read(endpoint));
        Assert.True(_effects.IsAttached(endpoint));
    }

    [Fact]
    public void BackedUp_ListsAttachedEndpointsOnly()
    {
        var first = _registry.AddEndpoint();
        var second = _registry.AddEndpoint();
        _registry.AddEndpoint();

        _effects.Attach(first);
        _effects.Attach(second);
        _effects.Detach(first);

        Assert.Equal([second], _effects.BackedUp());
    }

    [Theory]
    [InlineData("{1b9f7a52-3c8e-4d61-9a0b-5e2f7c4d8a13}", true)]
    [InlineData("1b9f7a52-3c8e-4d61-9a0b-5e2f7c4d8a13", false)]
    [InlineData(@"{1b9f7a52-3c8e-4d61-9a0b-5e2f7c4d8a13}\..\Render", false)]
    [InlineData("", false)]
    public void IsEndpoint_AcceptsOnlyBracedGuids(string value, bool expected)
    {
        Assert.Equal(expected, EndpointEffects.IsEndpoint(value));
    }
}
