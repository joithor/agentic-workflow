namespace Aw.Meta.Tests.Cli;

[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class TeamAndRoleCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Without_a_team_config_a_command_fails_with_one_AW_META_002_line_and_exit_2()
    {
        var result = await _cli.Run("team-show");

        Assert.Equal(2, result.Exit);
        Assert.StartsWith("AW-META-002: ", result.Err, StringComparison.Ordinal);
        Assert.Contains("aw-meta team-init", result.Err, StringComparison.Ordinal);
        _ = Assert.Single(result.Err.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Role_commands_and_team_init_work_without_a_team_config()
    {
        var before = await _cli.Run("role-show");
        var set = await _cli.Run("role-set", "--session", "s-1", "--name", "t1-meta");
        var after = await _cli.Run("role-show", "--json");
        var made = await _cli.Run("team-init", "--team", "t1", "--repo", "/work/repo", "--lanes", "api,ui");
        var shown = await _cli.Run("team-show");

        Assert.Equal(1, before.Exit);
        Assert.Equal(0, set.Exit);
        Assert.Contains("\"name\":\"t1-meta\"", after.Out.Replace(" ", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(0, made.Exit);
        Assert.Contains("lanes meta, api, ui", shown.Out, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_repeated_team_init_is_refused_with_its_code_and_a_usage_error_names_the_command()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "api");

        var again = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "api");
        var bad = await _cli.Run("role-set");

        Assert.Equal((2, "AW-META-011"), (again.Exit, again.Err[..11]));
        Assert.Equal(2, bad.Exit);
        Assert.Contains("usage: aw-meta role-set", bad.Err, StringComparison.Ordinal);
    }

    public void Dispose() => _cli.Dispose();
}
