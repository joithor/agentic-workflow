using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Ledger;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.Server;

/// <summary>An Error's extension members (RFC 9457 §3.2) through the pipeline Program runs: top-level members after the standard ones, every string leaf cleaned.</summary>
public sealed class ErrorExtensionsProblemTests : IDisposable
{
    private const int Deadline = 30_000;
    private readonly TempHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task Extensions_are_top_level_members_after_the_standard_ones_with_every_string_leaf_cleaned()
    {
        var ct = TestContext.Current.CancellationToken;
        var tooLong = new string('x', 300);
        var error = ErrorOf(KernelErrors.NotFound.With("no anchor 'intro'")
            .WithExtension("suggestedAnchors", Element($$"""[{"key":"a\u001b[31m","label":"line one\nline two"},{"key":"b","label":"{{tooLong}}"}]"""))
            .WithExtension("count", Element("2"))
            .WithExtension("closest", Element("""{"why":"tab\there","exact":true,"none":null}""")));

        var (status, body) = await Get(error, ct);
        var names = body.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(404, status);
        Assert.Equal("AW-KERNEL-002", body.GetProperty("title").GetString());
        Assert.Equal(["suggestedAnchors", "count", "closest"], names.Skip(names.IndexOf("suggestedAnchors")).Take(3).ToList());
        Assert.True(names.IndexOf("detail") < names.IndexOf("suggestedAnchors"));
        var anchors = body.GetProperty("suggestedAnchors");
        Assert.Equal("a\\x1B[31m", anchors[0].GetProperty("key").GetString());
        Assert.Equal("line one line two", anchors[0].GetProperty("label").GetString());
        var label = anchors[1].GetProperty("label").GetString()!;
        Assert.Equal(200, label.Length);
        Assert.EndsWith("…", label, StringComparison.Ordinal);
        Assert.Equal(2, body.GetProperty("count").GetInt32());
        Assert.Equal("tab here", body.GetProperty("closest").GetProperty("why").GetString());
        Assert.True(body.GetProperty("closest").GetProperty("exact").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("closest").GetProperty("none").ValueKind);
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task An_error_without_extensions_answers_with_exactly_the_standard_members()
    {
        var ct = TestContext.Current.CancellationToken;

        var (status, body) = await Get(ErrorOf(KernelErrors.NotFound.With("no anchor 'intro'")), ct);

        Assert.Equal(404, status);
        Assert.Equal(["type", "title", "status", "detail", "traceId"], body.EnumerateObject().Select(p => p.Name).ToList());
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task The_trace_id_is_still_last_a_secret_in_a_leaf_is_redacted_and_a_nested_member_name_is_escaped()
    {
        var ct = TestContext.Current.CancellationToken;
        var secret = string.Concat("sk", "-ant-", new string('q', 30));   // built at runtime: a literal key trips the repo's secret hook
        var error = ErrorOf(KernelErrors.NotFound.With("no anchor")
            .WithExtension("hint", Element($$"""{"note":"key {{secret}} here","a\u001bb":1}""")));

        var (_, body) = await Get(error, ct);
        var names = body.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal("traceId", names[^1]);
        Assert.Equal("hint", names[^2]);
        var hint = body.GetProperty("hint");
        Assert.DoesNotContain(secret, hint.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("qqqqqqqq", hint.GetRawText(), StringComparison.Ordinal);
        Assert.True(hint.TryGetProperty("a\\x1Bb", out _), hint.GetRawText());
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task An_array_is_cut_at_100_items_with_a_marker()
    {
        var ct = TestContext.Current.CancellationToken;
        var items = string.Join(',', Enumerable.Range(0, 130));
        var error = ErrorOf(KernelErrors.NotFound.With("many")
            .WithExtension("candidates", Element($"[{items}]")));

        var (_, body) = await Get(error, ct);
        var candidates = body.GetProperty("candidates");

        Assert.Equal(101, candidates.GetArrayLength());
        Assert.Equal(99, candidates[99].GetInt32());
        Assert.Equal("…(30 more)", candidates[100].GetString());
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task Names_that_clean_to_the_same_text_get_a_numbered_suffix()
    {
        var ct = TestContext.Current.CancellationToken;
        var tooLong = new string('n', 250);
        var error = ErrorOf(KernelErrors.NotFound.With("dup")
            .WithExtension("clash", Element($$"""{"{{tooLong}}1":1,"{{tooLong}}2":2,"{{tooLong}}3":3}""")));

        var (_, body) = await Get(error, ct);
        var names = body.GetProperty("clash").EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(3, names.Count);
        Assert.Equal(3, names.Distinct(StringComparer.Ordinal).Count());
        Assert.EndsWith("~2", names[1], StringComparison.Ordinal);
        Assert.EndsWith("~3", names[2], StringComparison.Ordinal);
    }

    [Fact(Timeout = Deadline), Trait("Lane", "integration")]
    public async Task A_middleware_refusal_carries_the_errors_extensions_too()
    {
        var ct = TestContext.Current.CancellationToken;
        var error = KernelErrors.NotFound.With("refused").WithExtension("why", Element("\"tab\\there\""));

        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        _ = app.MapGet("/probe-write", context => ApiProblems.Write(context, error, 503));
        await app.StartAsync(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };
        var answer = await http.GetAsync(new Uri("/probe-write", UriKind.Relative), ct);
        using var body = JsonDocument.Parse(await answer.Content.ReadAsStringAsync(ct));
        await app.StopAsync(ct);

        Assert.Equal(503, (int)answer.StatusCode);
        Assert.Equal("AW-KERNEL-002", body.RootElement.GetProperty("title").GetString());
        Assert.Equal("tab here", body.RootElement.GetProperty("why").GetString());
    }

    private static JsonElement Element(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static Result<Unit> ErrorOf(Error error) => error;

    private async Task<(int Status, JsonElement Body)> Get(Result<Unit> result, CancellationToken ct)
    {
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        _ = app.MapGet("/probe-ext", () => result.ToHttpResult());
        await app.StartAsync(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        var answer = await http.GetAsync(new Uri("/probe-ext", UriKind.Relative), ct);
        using var document = JsonDocument.Parse(await answer.Content.ReadAsStringAsync(ct));
        var body = document.RootElement.Clone();
        await app.StopAsync(ct);
        return ((int)answer.StatusCode, body);
    }
}
