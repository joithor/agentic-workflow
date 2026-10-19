using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Scoping;

namespace Aw.Sindri.Tests.Commands;

/// <summary>
/// 'sindri scoping check|show|desk' as Program runs them (scoping spec §8.2): the local --facts mode over a directory of fact files, the
/// refusals inside a git worktree (SND-SCOPING-002), and a serve that is down (SND-API-001, exit 2). It swaps the process's console.
/// </summary>
[Collection(ProcessEnvironment.Name)]
public sealed class ScopingCommandTests : IDisposable
{
    private readonly TempHome _home = new();
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();
    private readonly TextWriter _consoleOut = Console.Out;
    private readonly TextWriter _consoleError = Console.Error;

    public ScopingCommandTests()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
    }

    public void Dispose()
    {
        Console.SetOut(_consoleOut);
        Console.SetError(_consoleError);
        _home.Dispose();
    }

    private string Facts => Path.Combine(_home.Path, "facts");

    private async Task Put(ScopingFacts facts, string? directory = null)
    {
        var target = directory ?? Facts;
        _ = Directory.CreateDirectory(target);
        foreach (var (key, text) in facts)
        {
            await File.WriteAllTextAsync(Path.Combine(target, key + ".json"), text, TestContext.Current.CancellationToken);
        }
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Check_on_the_gold_facts_exits_0_and_json_prints_the_findings_shape()
    {
        await Put(ScopingFacts.Expiry());

        Assert.Equal(0, await Run("scoping", "check", "--facts", Facts, "--stage", "draft", "--json"));

        using var body = JsonDocument.Parse(_out.ToString());
        Assert.Equal("draft", body.RootElement.GetProperty("stage").GetString());
        Assert.Equal("pass", body.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(0, body.RootElement.GetProperty("findings").GetArrayLength());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Check_on_a_broken_fixture_exits_1_and_names_the_rule()
    {
        await Put(ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray()));

        Assert.Equal(1, await Run("scoping", "check", "--facts", Facts));

        Assert.Contains("SCP-ACC-001", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("fix:", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Check_defaults_to_the_draft_stage_and_runs_every_rule_at_evidence()
    {
        await Put(ScopingFacts.Expiry().Without("runs"));

        Assert.Equal(0, await Run("scoping", "check", "--facts", Facts));
        _out.GetStringBuilder().Clear();
        Assert.Equal(1, await Run("scoping", "check", "--facts", Facts, "--stage", "evidence"));
        Assert.Contains("SCP-CHAL-002", _out.ToString(), StringComparison.Ordinal);
        Assert.Contains("not run", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_draft_overlay_replaces_the_fact_files_it_names()
    {
        await Put(ScopingFacts.Expiry());
        var draft = Path.Combine(_home.Path, "run", "draft");
        await Put(new ScopingFacts { ["acceptance"] = """{"items":[]}""" }, draft);

        Assert.Equal(1, await Run("scoping", "check", "--facts", Facts, "--draft", draft));
        Assert.Contains("SCP-ACC-001", _out.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_draft_dir_inside_a_git_worktree_exits_2_with_SND_SCOPING_002()
    {
        await Put(ScopingFacts.Expiry());
        var worktree = Path.Combine(_home.Path, "checkout");
        _ = Directory.CreateDirectory(worktree);
        await File.WriteAllTextAsync(Path.Combine(worktree, ".git"), "gitdir: /elsewhere\n", TestContext.Current.CancellationToken);
        var draft = Path.Combine(worktree, "drafts");
        await Put(new ScopingFacts { ["acceptance"] = """{"items":[]}""" }, draft);

        Assert.Equal(2, await Run("scoping", "check", "--facts", Facts, "--draft", draft));

        Assert.Contains("SND-SCOPING-002", _error.ToString(), StringComparison.Ordinal);
        Assert.Contains("(fix: ", _error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_out.ToString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_prints_the_nine_sections_as_text_and_json_matches_the_golden()
    {
        await Put(ScopingShowTests.TwoLimitedLenses());

        Assert.Equal(0, await Run("scoping", "show", "--facts", Facts));
        var text = _out.ToString();
        Assert.Contains("2 of 4 lenses limited", text, StringComparison.Ordinal);
        Assert.Contains("not searched: history-unlinked — Wait for the ticket linker (S-5B)", text, StringComparison.Ordinal);

        _out.GetStringBuilder().Clear();
        Assert.Equal(0, await Run("scoping", "show", "--facts", Facts, "--json"));
        using var body = JsonDocument.Parse(_out.ToString());
        Assert.Equal(9, body.RootElement.GetProperty("sections").GetArrayLength());
        Assert.Equal("2 of 4 lenses limited", body.RootElement.GetProperty("coverageHeader").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Show_fences_the_untrusted_text_for_a_session()
    {
        await Put(ScopingFacts.Expiry());
        _session = true;   // AW_SINDRI_CHILD makes the caller a session whatever process runs the test

        Assert.Equal(0, await Run("scoping", "show", "--facts", Facts));

        var shown = _out.ToString();
        Assert.Contains("<untrusted source=\"scoping\">AC-1 [ears] When a member shares a note and picks an expiry time", shown, StringComparison.Ordinal);   // acceptance text
        Assert.Contains("<untrusted source=\"scoping\">▣ S1 feature:sharing — Public link sharing</untrusted>", shown, StringComparison.Ordinal);   // a map title drawn from a lens
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_check_writes_the_report_and_appends_rounds_to_the_out_dir()
    {
        await Put(ScopingFacts.Expiry().Without("rounds"));
        var outDir = Path.Combine(_home.Path, "run");

        Assert.Equal(0, await Run("scoping", "desk", "check", "--facts", Facts, "--out", outDir));
        Assert.Equal(0, await Run("scoping", "desk", "check", "--facts", Facts, "--out", outDir));

        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outDir, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("pass", report.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("step.scope.check", report.RootElement.GetProperty("block").GetString());
        var rounds = (await File.ReadAllLinesAsync(Path.Combine(outDir, "rounds.jsonl"), TestContext.Current.CancellationToken)).Select(static l => JsonNode.Parse(l)!).ToList();
        Assert.Equal([1, 2], rounds.Select(static r => r["round"]!.GetValue<int>()));
        Assert.Equal(["first", "revise"], rounds.Select(static r => r["trigger"]!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal(10, rounds[0]["added"]!.AsArray().Count);   // the first round adds every item
        Assert.Empty(rounds[1]["added"]!.AsArray());   // the second diffs against the first, which only the out directory holds
        Assert.Empty(rounds[1]["changed"]!.AsArray());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_check_reports_the_findings_as_a_fact_file_the_complete_verb_can_take()
    {
        await Put(ScopingFacts.Expiry().Edit("acceptance", n => n["items"] = new JsonArray()));
        var outDir = Path.Combine(_home.Path, "run");

        Assert.Equal(1, await Run("scoping", "desk", "check", "--facts", Facts, "--out", outDir));

        using var report = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outDir, "report.json"), TestContext.Current.CancellationToken));
        Assert.Equal("gaps", report.RootElement.GetProperty("outcome").GetString());
        Assert.Equal("findings.json", report.RootElement.GetProperty("outputs").GetProperty("fact:findings").GetString());
        Assert.True(File.Exists(Path.Combine(outDir, "findings.json")));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Desk_out_inside_a_git_worktree_exits_2_with_SND_SCOPING_002()
    {
        await Put(ScopingFacts.Expiry());
        var worktree = Path.Combine(_home.Path, "checkout");
        _ = Directory.CreateDirectory(Path.Combine(worktree, ".git"));

        Assert.Equal(2, await Run("scoping", "desk", "check", "--facts", Facts, "--out", Path.Combine(worktree, "out")));

        Assert.Contains("SND-SCOPING-002", _error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(worktree, "out")));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("check")]
    [InlineData("show")]
    public async Task With_serve_down_an_address_exits_2_with_SND_API_001_and_never_starts_it(string verb)
    {
        Assert.Equal(2, await Run("scoping", verb, "J-1"));

        Assert.Contains("SND-API-001", _error.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(_home.Path, "control.json")));
    }

    [Theory, Trait("Lane", "integration")]
    [InlineData("check")]
    [InlineData("show")]
    public async Task Facts_and_an_address_together_or_neither_is_a_usage_error(string verb)
    {
        Assert.Equal(2, await Run("scoping", verb, "J-1", "--facts", Facts));
        Assert.Equal(2, await Run("scoping", verb));
        Assert.Contains("usage: sindri scoping", _error.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "integration")]
    public async Task A_facts_dir_that_does_not_exist_is_exit_2_with_a_fix()
    {
        Assert.Equal(2, await Run("scoping", "check", "--facts", Path.Combine(_home.Path, "nowhere")));

        Assert.Contains("(fix: ", _error.ToString(), StringComparison.Ordinal);
    }

    private bool _session;

    private string? Environment(string name) => name switch { "AW_SINDRI_HOME" => _home.Path, "AW_SINDRI_CHILD" when _session => "1", _ => null };

    private Task<int> Run(params string[] args) => CommandTable.Run(args, Environment, TimeProvider.System);
}
