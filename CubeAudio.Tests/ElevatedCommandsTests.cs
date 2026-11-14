using CubeAudio.Boost;

namespace CubeAudio.Tests;

public class ElevatedCommandsTests
{
    [Theory]
    [InlineData(ElevatedCommands.Attach, "{1b9f7a52-3c8e-4d61-9a0b-5e2f7c4d8a13}")]
    [InlineData(ElevatedCommands.Detach, "{1b9f7a52-3c8e-4d61-9a0b-5e2f7c4d8a13}")]
    [InlineData(ElevatedCommands.DetachAll)]
    public void IsCommand_KnownCommand_IsTrue(params string[] arguments)
    {
        Assert.True(ElevatedCommands.IsCommand(arguments));
    }

    [Theory]
    [InlineData]
    [InlineData("--help")]
    [InlineData(@"C:\some\file.wav")]
    public void IsCommand_AnythingElse_IsFalse(params string[] arguments)
    {
        Assert.False(ElevatedCommands.IsCommand(arguments));
    }
}
