using System.Net;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Shell;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Shell;

/// <summary>Health's Code index card (code-index spec §7.3, CI-10b): the words keyed by the index's state, from the status read.</summary>
[Lane(TestLane.Unit)]
public sealed class CodeIndexCardTests
{
    private const string Sha = "abcdef0123456789abcdef0123456789abcdef01";
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private readonly FakeSindriApi _server = new();
    private readonly FakeTimeProvider _clock = new(A.Noon);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Layer(string layer, string state, string? code = null, string detail = "ok") =>
        $$"""{"layer":"{{layer}}","state":"{{state}}","code":{{(code is null ? "null" : "\"" + code + "\"")}},"detail":"{{detail}}"}""";

    private string Repo(string name, string state, string? commit, int builtMinutesAgo, params string[] layers) =>
        $$"""{"repo":"{{name}}","state":"{{state}}","commit":{{(commit is null ? "null" : "\"" + commit + "\"")}},"builtAt":"{{_clock.GetUtcNow().AddMinutes(-builtMinutesAgo):O}}","layers":[{{string.Join(",", layers)}}],"counts":null}""";

    private async Task<CodeIndexCardViewModel> Read(params string[] repos)
    {
        _server.CodeIndexStatusAnswer = new RouteAnswer(HttpStatusCode.OK, $$"""{"repos":[{{string.Join(",", repos)}}]}""");
        var card = new CodeIndexCardViewModel(_server, _clock);
        await card.Refresh(_api, Token);
        return card;
    }

    [Fact]
    public async Task A_healthy_index_reads_repo_commit_age_and_each_layer()
    {
        var card = await Read(Repo("repo", "ok", Sha, 3, Layer("structure", "ok"), Layer("graph", "ok"), Layer("embeddings", "pending", detail: "31 of 100 embedded")));

        var line = Assert.Single(card.Lines);
        Assert.Equal("repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%", line.Text);
        Assert.Equal("Code index: repo @ abcdef0, 3 min old, structure ok · graph ok · embeddings pending 31%", line.Name);
        Assert.Equal((Labels.CodeIndexOk, string.Empty, false), (card.Word, card.Message, card.HasMessage));
    }

    [Fact]
    public async Task A_layer_that_cannot_be_built_says_unavailable_with_its_code_and_the_card_needs_a_look()
    {
        var card = await Read(Repo("repo", "ok", Sha, 3, Layer("structure", "ok"), Layer("graph", "unavailable", "SND-CODEIDX-020"), Layer("embeddings", "disabled")));

        Assert.Equal("repo @ abcdef0, 3 min old, structure ok · graph unavailable (SND-CODEIDX-020) · embeddings off", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task Clones_and_deps_show_only_when_they_need_a_look()
    {
        var card = await Read(Repo("repo", "ok", Sha, 1, Layer("structure", "ok"), Layer("clones", "ok"), Layer("deps", "unavailable", "SND-CODEIDX-021")));

        Assert.Equal("repo @ abcdef0, 1 min old, structure ok · deps unavailable (SND-CODEIDX-021)", Assert.Single(card.Lines).Text);
    }

    [Theory]
    [InlineData("missing", "repo: first build queued")]
    [InlineData("damaged", "repo: damaged, rebuilding on the next check")]
    [InlineData("building", "repo: building")]
    [InlineData("waiting", "repo: waiting for a free build slot")]
    [InlineData("something-new", "repo: something-new")]
    public async Task An_index_with_no_commit_says_its_state_in_words(string state, string expected)
    {
        var card = await Read(Repo("repo", state, commit: null, 0));

        Assert.Equal(expected, Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task A_stale_index_names_the_state_beside_its_age()
    {
        var card = await Read(Repo("repo", "stale", Sha, 120, Layer("structure", "ok")));

        Assert.Equal("repo @ abcdef0, 2 h old (behind main), structure ok", Assert.Single(card.Lines).Text);
        Assert.Equal(Labels.CodeIndexNeedsLook, card.Word);
    }

    [Fact]
    public async Task Each_repo_has_its_own_line()
    {
        var card = await Read(Repo("a", "ok", Sha, 5, Layer("structure", "ok")), Repo("b", "missing", commit: null, 0));

        Assert.Equal(["a @ abcdef0, 5 min old, structure ok", "b: first build queued"], card.Lines.Select(l => l.Text));
    }

    [Fact]
    public async Task No_repos_says_so()
    {
        var card = await Read();

        Assert.Empty(card.Lines);
        Assert.Equal((Labels.CodeIndexNone, string.Empty), (card.Message, card.Word));
    }

    [Fact]
    public async Task A_failed_read_says_it_cannot_be_read_and_drops_the_old_lines()
    {
        var card = await Read(Repo("repo", "ok", Sha, 3, Layer("structure", "ok")));
        _server.CodeIndexStatusAnswer = RouteAnswer.Problem(HttpStatusCode.ServiceUnavailable, "SND-API-001", "not running");

        await card.Refresh(_api, Token);

        Assert.Empty(card.Lines);
        Assert.Equal((Labels.CodeIndexUnreadable, true), (card.Message, card.HasMessage));
    }

    [Fact]
    public void Before_the_first_read_it_says_it_is_reading()
    {
        var card = new CodeIndexCardViewModel(_server, _clock);

        Assert.Equal((Labels.ReadingCodeIndex, 0), (card.Message, card.Lines.Count));
    }
}
