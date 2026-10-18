using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.RegisterMcp;
using static Aw.Sindri.Features.RegisterMcp.ProviderPlans;

namespace Aw.Sindri.Tests.RegisterMcp;

[Lane(TestLane.Unit)]
public sealed class ProviderPlansTests
{
    private const string Url = "http://127.0.0.1:7480/sindri/mcp";

    private static string ClaudeJson(string url, string repo) =>
        $$"""{"projects":{"/work/demo":{"mcpServers":{"sindri":{"type":"http","url":"{{url}}","headers":{"X-Sindri-Repo":"{{repo}}"}}}}}}""";

    [Fact]
    public void Claude_entry_is_absent_same_or_different()
    {
        Assert.Equal(ClaudeState.Absent, Claude(null, "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Absent, Claude("""{"projects":{}}""", "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Same, Claude(ClaudeJson(Url, "demo"), "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Different, Claude(ClaudeJson("http://127.0.0.1:7481/sindri/mcp", "demo"), "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Different, Claude(ClaudeJson(Url, "other"), "/work/demo", Url, "demo"));
    }

    [Fact]
    public void Cursor_creates_the_file_and_leaves_other_servers_alone()
    {
        var created = Cursor(null, Url, "demo");
        var merged = Cursor("""{"mcpServers":{"other":{"command":"x"}}}""", Url, "demo");

        Assert.Null(created.Problem);
        var entry = JsonNode.Parse(created.Text!)!["mcpServers"]!["sindri"]!;
        Assert.Equal(Url, (string?)entry["url"]);
        Assert.Equal("demo", (string?)entry["headers"]!["X-Sindri-Repo"]);
        var other = JsonNode.Parse(merged.Text!)!["mcpServers"]!;
        Assert.Equal("x", (string?)other["other"]!["command"]);
        Assert.NotNull(other["sindri"]);
    }

    [Fact]
    public void Cursor_is_unchanged_when_right_replaced_when_the_port_moved_and_refused_when_not_json()
    {
        var written = Cursor(null, Url, "demo").Text!;

        Assert.Equal(new FilePlan(null, null), Cursor(written, Url, "demo"));
        var moved = Cursor(written, "http://127.0.0.1:7481/sindri/mcp", "demo");
        Assert.Contains("7481", moved.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("7480", moved.Text, StringComparison.Ordinal);
        Assert.NotNull(Cursor("{ nope", Url, "demo").Problem);
    }

    [Fact]
    public void Codex_appends_the_table_and_keeps_the_rest()
    {
        var plan = Codex("model = \"x\"\n\n[mcp_servers.other]\ncommand = \"y\"\n", Url, "demo");

        Assert.Null(plan.Problem);
        Assert.Contains("model = \"x\"", plan.Text, StringComparison.Ordinal);
        Assert.Contains("[mcp_servers.other]\ncommand = \"y\"", plan.Text, StringComparison.Ordinal);
        Assert.Contains("[mcp_servers.sindri]\nurl = \"" + Url + "\"\nhttp_headers = { \"X-Sindri-Repo\" = \"demo\" }", plan.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Codex_is_unchanged_when_right_and_rewrites_only_its_own_table_when_the_port_moved()
    {
        var written = Codex("[mcp_servers.other]\ncommand = \"y\"\n", Url, "demo").Text!;

        Assert.Equal(new FilePlan(null, null), Codex(written, Url, "demo"));
        var moved = Codex(written + "\n[tail]\na = 1\n", "http://127.0.0.1:7481/sindri/mcp", "demo").Text!;
        Assert.Contains("7481", moved, StringComparison.Ordinal);
        Assert.DoesNotContain("7480", moved, StringComparison.Ordinal);
        Assert.Contains("[mcp_servers.other]", moved, StringComparison.Ordinal);
        Assert.Contains("[tail]\na = 1", moved, StringComparison.Ordinal);
        Assert.Equal(1, moved.Split("[mcp_servers.sindri]").Length - 1);
    }

    [Fact]
    public void Codex_refuses_an_inline_mcp_servers_table_it_cannot_edit()
    {
        Assert.NotNull(Codex("mcp_servers = { sindri = { url = \"x\" } }\n", Url, "demo").Problem);
    }

    [Fact]
    public void Codex_trust_is_read_from_the_user_config_and_the_row_is_appended()
    {
        const string trusted = "[projects.\"/work/demo\"]\ntrust_level = \"trusted\"\n";

        Assert.True(CodexTrusted(trusted, "/work/demo"));
        Assert.False(CodexTrusted(trusted, "/work/other"));
        Assert.False(CodexTrusted("[projects.\"/work/demo\"]\ntrust_level = \"untrusted\"\n", "/work/demo"));
        Assert.False(CodexTrusted(null, "/work/demo"));
        Assert.True(CodexTrusted(CodexTrustRow("model = \"x\"\n", "/work/demo"), "/work/demo"));
    }
}
