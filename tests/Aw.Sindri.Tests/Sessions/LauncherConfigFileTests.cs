using System.Text;
using System.Text.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Launch;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>The launcher.json reader and the agent catalog (collector spec CA-0): the same verdicts as the schema, built-ins merged with the file's entries.</summary>
public sealed class LauncherConfigFileTests
{
    private static byte[] Text(string json) => Encoding.UTF8.GetBytes(json);

    [Fact, Trait("Lane", "unit")]
    public void Catalog_merges_launcher_json_agents_over_builtins()
    {
        var user = Text("""{ "schema": 1, "agents": { "aider": { "label": "Aider", "bin": "aider", "open": ["{bin}", "--message", "{seed}"], "sessionId": "none", "processName": "aider" }, "claude-code": { "override": true, "bin": "/opt/claude/bin/claude" } } }""");

        var config = LauncherConfigFile.Read(SessionFixtures.Builtins(), user).Value;
        var catalog = new AgentCatalog(config.Agents);

        Assert.Equal(["claude-code", "codex", "cursor", "aider"], catalog.All.Select(static a => a.Id.Value), StringComparer.Ordinal);
        var claude = Assert.IsType<AgentEntry>(catalog.Resolve(AgentId.From("claude-code")));
        Assert.Equal(("Claude Code", "/opt/claude/bin/claude", "claude"), (claude.Label, claude.Bin, claude.ProcessName));
        Assert.Equal("aider", catalog.Resolve(AgentId.From("aider"))?.MatchName);
        Assert.Equal("codex", catalog.Resolve(AgentId.From("codex"))?.MatchName);
        Assert.Null(catalog.Resolve(AgentId.From("nothing")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Builtins_alone_give_the_three_agents_with_their_process_names_and_transcripts()
    {
        var entries = LauncherConfigFile.Read(SessionFixtures.Builtins(), user: null).Value.Agents;

        Assert.Equal([("claude-code", "claude"), ("codex", "codex"), ("cursor", "agent")], entries.Select(static e => (e.Id.Value, e.MatchName)));
        Assert.Equal("~/.claude/projects/*/{sessionId}.jsonl", entries[0].Transcripts);
        Assert.Equal(["{bin}", "--resume", "{sessionId}"], entries[0].Resume);
        Assert.StartsWith("rollout-", entries[1].IdCapture, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_launcher_fixture_gets_the_schemas_verdict()
    {
        var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(SessionFixtures.Launcher, "manifest.json")));
        var builtins = SessionFixtures.Builtins();

        foreach (var fixture in manifest.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            var file = fixture.GetProperty("file").GetString()!;
            var expected = fixture.GetProperty("schema").GetString() == "valid";

            var read = LauncherConfigFile.Read(builtins, File.ReadAllBytes(Path.Combine(SessionFixtures.Launcher, file)));

            Assert.True(read.IsSuccess == expected, $"{file}: expected {(expected ? "valid" : "invalid")}, got {(read.IsError ? read.Error.Message : "valid")}");
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void A_sessions_section_with_an_unknown_key_or_a_poll_out_of_range_is_refused()
    {
        var bogus = LauncherConfigFile.Read(SessionFixtures.Builtins(), Text("""{ "schema": 1, "sessions": { "nope": 1 } }"""));
        var range = LauncherConfigFile.Read(SessionFixtures.Builtins(), Text("""{ "schema": 1, "sessions": { "pollSeconds": 31 } }"""));

        Assert.Equal(["SND-LAUNCH-011", "SND-LAUNCH-011"], [bogus.Error.Code, range.Error.Code]);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_viewer_host_and_meta_sections_are_accepted_and_not_read()
    {
        var config = LauncherConfigFile.Read(SessionFixtures.Builtins(), Text("""
            { "schema": 1, "viewer": { "kind": "print" }, "host": { "kind": "none" }, "meta": { "lanesAgent": "codex", "lanes": { "review": "claude-code" } } }
            """)).Value;

        Assert.Equal(3, config.Agents.Count);
        Assert.Null(config.Sessions);
    }

    [Fact, Trait("Lane", "integration")]
    public void Startup_reads_the_sessions_section_and_lets_the_test_override_replace_the_records_dir()
    {
        var settings = new SindriSettings(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "profile.yaml"));
        var file = Path.Combine(SessionFixtures.Launcher, "sessions.json");
        string? Env(string name) => name switch { "AW_LAUNCHER_CONFIG" => file, _ => null };
        string? Overridden(string name) => name == "AW_SESSIONS_CLAUDE_DIR" ? "/nonexistent-fixture-root/claude-sessions" : Env(name);

        var plain = LauncherStartup.Load(Env, settings);
        var overridden = LauncherStartup.Load(Overridden, settings);

        Assert.Equal((true, 5, false, null), (plain.Sessions.Enabled, plain.Sessions.PollSeconds, plain.Sessions.TestOverride, plain.Sessions.ConfigProblem));
        Assert.Equal(["claude-code", "codex"], plain.Sessions.Providers!.Select(static p => p.Value), StringComparer.Ordinal);
        Assert.Equal("/home/dev/.config/claude/sessions", plain.ClaudeSessionsDirectory);
        Assert.Equal(("/nonexistent-fixture-root/claude-sessions", true), (overridden.ClaudeSessionsDirectory, overridden.Sessions.TestOverride));
    }

    [Fact, Trait("Lane", "integration")]
    public void An_invalid_launcher_json_leaves_the_builtins_and_says_so()
    {
        var settings = new SindriSettings(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "profile.yaml"));
        var file = Path.Combine(SessionFixtures.Launcher, "sessions-bogus.json");

        var load = LauncherStartup.Load(name => name == "AW_LAUNCHER_CONFIG" ? file : null, settings);

        Assert.Equal(3, load.Catalog.All.Count);
        Assert.NotNull(load.Sessions.ConfigProblem);
        Assert.True(load.Sessions.Enabled);
    }
}
