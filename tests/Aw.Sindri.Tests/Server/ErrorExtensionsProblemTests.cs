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
            .WithExtension("suggestedAnchors", JsonDocument.Parse($$"""[{"key":"a\u001b[31m","label":"line one\nline two"},{"key":"b","label":"{{tooLong}}"}]""").RootElement)
            .WithExtension("count", JsonDocument.Parse("2").RootElement)
            .WithExtension("closest", JsonDocument.Parse("""{"why":"tab\there","exact":true,"none":null}""").RootElement));

        var (status, body) = await Get(error, ct);
        var names = body.EnumerateObject().Select(p => p.Name).ToList();

        Assert.Equal(404, status);
        Assert.Equal("AW-KERNEL-002", body.GetProperty("title").GetString());
        Assert.Equal(["suggestedAnchors", "count", "closest"], names.Skip(names.IndexOf("suggestedAnchors")).ToList());
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

    private static Result<Unit> ErrorOf(Error error) => error;

    private async Task<(int Status, JsonElement Body)> Get(Result<Unit> result, CancellationToken ct)
    {
        await using var app = LoopbackBinding.Build([], n => n switch { "AW_SINDRI_HOME" => _home.Path, _ => null }, TimeProvider.System, NoTemporal.Use);
        _ = Pipeline.Configure(app);
        _ = app.MapGet("/probe-ext", () => result.ToHttpResult());
        await app.StartAsync(ct);
        using var http = new HttpClient { BaseAddress = new Uri(app.Urls.First()), Timeout = TimeSpan.FromSeconds(10) };

        var answer = await http.GetAsync(new Uri("/probe-ext", UriKind.Relative), ct);
        var body = JsonDocument.Parse(await answer.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        await app.StopAsync(ct);
        return ((int)answer.StatusCode, body);
    }
}
