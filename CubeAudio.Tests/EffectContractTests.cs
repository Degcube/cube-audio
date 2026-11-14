using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using CubeAudio.Boost;

namespace CubeAudio.Tests;

public partial class EffectContractTests
{
    [Fact]
    public void Constants_MatchTheHeaderOfTheEffect()
    {
        var header = HeaderConstants();
        var app = typeof(EffectContract)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .ToDictionary(field => field.Name, field => Convert.ToString(field.GetRawConstantValue())!);

        Assert.NotEmpty(header);
        Assert.Equal(header.Keys.Order(), app.Keys.Order());

        foreach (var (name, value) in header)
            Assert.Equal(value, app[name]);
    }

    private static Dictionary<string, string> HeaderConstants([CallerFilePath] string testFile = "")
    {
        var header = Path.Combine(Path.GetDirectoryName(testFile)!, "..", "CubeAudio.Apo", "Contract.h");
        var constants = new Dictionary<string, string>();

        foreach (Match match in Constant().Matches(File.ReadAllText(header)))
        {
            var value = match.Groups["text"].Success
                ? match.Groups["text"].Value.Replace(@"\\", @"\")
                : match.Groups["number"].Value;

            constants.Add(match.Groups["name"].Value, value);
        }

        return constants;
    }

    [GeneratedRegex("""inline constexpr (?:wchar_t (?<name>\w+)\[\] = L"(?<text>[^"]*)"|unsigned long (?<name>\w+) = (?<number>\d+));""")]
    private static partial Regex Constant();
}
