using System.Text.Json;

namespace Aw.Meta.Tests.Cli;

/// <summary>The non-Claude lane fields are additive: a team without a non-Claude lane gets the JSON it always got.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class ClaudeOnlyJsonTests : IDisposable
{
    private readonly CliHarness _cli = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Health_and_lanes_json_leave_out_the_non_Claude_fields_for_a_Claude_only_team()
    {
        _ = await _cli.Run("team-init", "--team", "t1", "--repo", "/r", "--lanes", "meta,api");
        _ = await _cli.Run("role-set", "--session", "s-meta", "--name", "t1-meta");

        var health = await _cli.Run("health", "--json");
        var lanes = await _cli.Run("lanes", "--json");

        using var healthJson = JsonDocument.Parse(health.Out);
        using var lanesJson = JsonDocument.Parse(lanes.Out);
        Assert.True(healthJson.RootElement.TryGetProperty("attention", out _));   // it is the health document
        Assert.False(healthJson.RootElement.TryGetProperty("lanes", out _));
        Assert.True(lanesJson.RootElement.TryGetProperty("entries", out _));      // and the lane map
        Assert.False(lanesJson.RootElement.TryGetProperty("standing", out _));
    }

    public void Dispose() => _cli.Dispose();
}
