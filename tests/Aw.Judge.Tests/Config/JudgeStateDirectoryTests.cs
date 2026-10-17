using Aw.Judge.Infrastructure;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.Config;

[Lane(TestLane.Unit)]
public sealed class JudgeStateDirectoryTests
{
    private static string? Read(Dictionary<string, string> env) =>
        SettingsFromEnvironment.Read(name => env.GetValueOrDefault(name)).StateDirectory;

    [Fact]
    public void AW_JUDGE_STATE_DIR_moves_only_the_judge_directory()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_STATE_DIR"] = "/srv/sindri/judge", ["AW_STATE_DIR"] = "/srv/box" };

        Assert.Equal("/srv/sindri/judge", Read(env));   // why: used as the judge directory itself, not <root>/judge
    }

    [Fact]
    public void Without_it_the_directory_is_under_AW_STATE_DIR()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_STATE_DIR"] = "/srv/box" };

        Assert.Equal(Path.Combine("/srv/box", "judge"), Read(env));
    }

    [Fact]
    public void An_empty_AW_JUDGE_STATE_DIR_is_ignored()
    {
        var env = new Dictionary<string, string>(StringComparer.Ordinal) { ["AW_JUDGE_STATE_DIR"] = "", ["AW_STATE_DIR"] = "/srv/box" };

        Assert.Equal(Path.Combine("/srv/box", "judge"), Read(env));
    }
}
