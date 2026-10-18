using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using static Aw.Sindri.Features.RegisterMcp.ProviderPlans;

namespace Aw.Sindri.Tests.RegisterMcp;

[Lane(TestLane.Unit)]
public sealed class ProviderPlansTests
{
    private const string Url = "http://127.0.0.1:7480/sindri/mcp";

    private static string ClaudeJson(string url, string repo) =>
        $$"""{"projects":{"/work/demo":{"mcpServers":{"sindri":{"type":"http","url":"{{url}}","headers":{"X-Sindri-Repo":"{{repo}}"} } } } } }""";

    [Fact]
    public void Claude_entry_is_absent_same_or_different()
    {
        Assert.Equal(ClaudeState.Absent, Claude(claudeJson: null, "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Absent, Claude("""{"projects":{}}""", "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Same, Claude(ClaudeJson(Url, "demo"), "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Different, Claude(ClaudeJson("http://127.0.0.1:7481/sindri/mcp", "demo"), "/work/demo", Url, "demo"));
        Assert.Equal(ClaudeState.Different, Claude(ClaudeJson(Url, "other"), "/work/demo", Url, "demo"));
    }

    [Fact]
    public void Cursor_creates_the_file_and_leaves_other_servers_alone()
    {
        var created = Cursor(existing: null, Url, "demo");
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
        var written = Cursor(existing: null, Url, "demo").Text;

        Assert.Equal(new FilePlan(Text: null, Problem: null), Cursor(written, Url, "demo"));
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

        Assert.Equal(new FilePlan(Text: null, Problem: null), Codex(written, Url, "demo"));
        var moved = Codex(written + "\n[tail]\na = 1\n", "http://127.0.0.1:7481/sindri/mcp", "demo").Text!;
        Assert.Contains("7481", moved, StringComparison.Ordinal);
        Assert.DoesNotContain("7480", moved, StringComparison.Ordinal);
        Assert.Contains("[mcp_servers.other]", moved, StringComparison.Ordinal);
        Assert.Contains("[tail]\na = 1", moved, StringComparison.Ordinal);
        Assert.Equal(1, moved.Split("[mcp_servers.sindri]").Length - 1);
    }

    [Fact]
    public void Codex_refuses_an_inline_mcp_servers_table_it_cannot_edit() =>
        Assert.NotNull(Codex("mcp_servers = { sindri = { url = \"x\" } }\n", Url, "demo").Problem);

    [Fact]
    public void Codex_trust_level_is_read_from_the_user_config()
    {
        const string trusted = "[projects.\"/work/demo\"]\ntrust_level = \"trusted\"\n";

        Assert.Equal("trusted", CodexTrust(trusted, "/work/demo"));
        Assert.Null(CodexTrust(trusted, "/work/other"));
        Assert.Equal("untrusted", CodexTrust("[projects.\"/work/demo\"]\ntrust_level = \"untrusted\"\n", "/work/demo"));
        Assert.Null(CodexTrust(userConfig: null, "/work/demo"));
    }

    [Fact]
    public void The_trust_row_is_appended_and_touches_nothing_else()
    {
        const string user = "# my codex\nmodel = \"x\"   # keep\n\n[projects.\"/work/other\"]\ntrust_level = \"trusted\"\n\n[mcp_servers.fs]\ncommand = \"npx\"\n";

        var after = CodexTrustRow(user, "/work/demo");

        Assert.StartsWith(user, after, StringComparison.Ordinal);
        Assert.Equal("trusted", CodexTrust(after, "/work/demo"));
        Assert.Equal("trusted", CodexTrust(after, "/work/other"));
        Assert.Equal("[projects.\"/work/demo\"]\ntrust_level = \"trusted\"\n", after[user.Length..].TrimStart('\n'));
    }

    [Fact]
    public void A_project_table_without_a_level_gets_the_line_inside_it_not_a_second_table()
    {
        var after = CodexTrustRow("[projects.\"/work/demo\"]\nnote = 1\n[other]\na = 1\n", "/work/demo");

        Assert.Equal("[projects.\"/work/demo\"]\ntrust_level = \"trusted\"\nnote = 1\n[other]\na = 1\n", after);
        Assert.Equal(1, after.Split("[projects.").Length - 1);
    }

    [Theory]
    [InlineData("/work/my\"repo")]
    [InlineData("C:\\work\\repo")]
    [InlineData("/work/my repo")]
    public void A_trust_row_for_a_path_with_a_quote_backslash_or_space_is_found_on_the_next_run(string checkout)
    {
        var row = CodexTrustRow(userConfig: null, checkout);

        Assert.Equal("trusted", CodexTrust(row, checkout));
        Assert.Null(CodexTrust(row, checkout + "x"));
    }

    [Fact]
    public void A_port_change_keeps_the_keys_and_comments_the_owner_added_to_the_sindri_table()
    {
        const string existing = "[mcp_servers.sindri]\n# mine\nurl = \"http://127.0.0.1:7000/sindri/mcp\"\nstartup_timeout_sec = 30\nhttp_headers = { \"X-Sindri-Repo\" = \"demo\" }\nenabled = true\n\n[mcp_servers.sindri.env]\nA = \"1\"\n";

        var moved = Codex(existing, Url, "demo").Text!;

        Assert.Contains("# mine", moved, StringComparison.Ordinal);
        Assert.Contains("startup_timeout_sec = 30", moved, StringComparison.Ordinal);
        Assert.Contains("enabled = true", moved, StringComparison.Ordinal);
        Assert.Contains("[mcp_servers.sindri.env]\nA = \"1\"", moved, StringComparison.Ordinal);
        Assert.Contains("url = \"" + Url + "\"", moved, StringComparison.Ordinal);
        Assert.DoesNotContain("7000", moved, StringComparison.Ordinal);
        Assert.Equal(1, moved.Split("[mcp_servers.sindri]").Length - 1);
        Assert.Equal(new FilePlan(Text: null, Problem: null), Codex(moved, Url, "demo"));
    }

    [Fact]
    public void A_sindri_table_header_with_a_trailing_comment_is_recognised()
    {
        var existing = "[mcp_servers.sindri] # mine\nurl = \"" + Url + "\"\nhttp_headers = { \"X-Sindri-Repo\" = \"demo\" }\n";

        Assert.Equal(new FilePlan(Text: null, Problem: null), Codex(existing, Url, "demo"));
        Assert.Equal(1, Codex(existing, "http://127.0.0.1:7481/sindri/mcp", "demo").Text!.Split("[mcp_servers.").Length - 1);
    }

    [Theory]
    [InlineData("mcp_servers.sindri.url = \"x\"\n")]
    [InlineData("[mcp_servers]\nsindri.url = \"x\"\n")]
    [InlineData("[mcp_servers.sindri.http_headers]\nX-Sindri-Repo = \"demo\"\n")]
    public void Codex_refuses_a_dotted_or_table_form_it_cannot_edit(string existing) =>
        Assert.NotNull(Codex(existing, Url, "demo").Problem);

    [Fact]
    public void A_sibling_server_whose_name_starts_with_sindri_is_not_mistaken_for_it()
    {
        var plan = Codex("[mcp_servers]\nsindri2 = { url = \"x\" }\n", Url, "demo");

        Assert.Null(plan.Problem);
        Assert.Contains("sindri2 = { url = \"x\" }", plan.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"a\": 1, \"a\": 2}")]
    [InlineData("{\"mcpServers\": {\"x\": {}, \"x\": {}}}")]
    public void A_cursor_file_with_a_duplicate_key_is_a_problem_not_a_crash(string existing) =>
        Assert.NotNull(Cursor(existing, Url, "demo").Problem);

    [Fact]
    public void The_cursor_file_keeps_other_servers_values_unescaped() =>
        Assert.Contains("\"a+b <c>\"", Cursor("{\"mcpServers\": {\"x\": {\"command\": \"a+b <c>\"}}}", Url, "demo").Text, StringComparison.Ordinal);
}
