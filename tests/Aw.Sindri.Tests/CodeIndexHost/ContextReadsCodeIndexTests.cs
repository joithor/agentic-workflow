using System.Net;
using System.Text.Json;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Tests.ApiGoldens;
using Aw.Sindri.Tests.ContextCalls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.CodeIndexHost;

/// <summary>The code lens over a host whose code index is a fake (S-9): callers and callees come from the index and the 'code-index' source reads ok at the map's commit, stale (degraded) at another.</summary>
public sealed class ContextReadsCodeIndexTests : IDisposable
{
    private static readonly RepoName _repoName = RepoName.From(CodeLensRepo.Repo);
    private static readonly SymbolRef _create = SymbolRef.From($"{CodeLensRepo.Repo}:src/sharing/links.ts#createLink");
    private static readonly SymbolRef _route = SymbolRef.From($"{CodeLensRepo.Repo}:src/sharing/routes.ts#postLink");
    private readonly CodeLensRepo _repo = CodeLensRepo.Build();

    public void Dispose() => _repo.Dispose();

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task The_code_lens_fills_callers_and_callees_from_the_index_and_the_code_index_source_is_ok()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host(indexedAt: _repo.Head);

        var view = await Code(host);

        var sources = view.GetProperty("searched").GetProperty("sources").EnumerateArray().ToDictionary(s => s.GetProperty("id").GetString()!, s => s.GetProperty("state").GetString(), StringComparer.Ordinal);
        Assert.Equal("ok", sources["code-index"]);
        Assert.Equal("ok", view.GetProperty("callers").GetProperty("state").GetString());
        Assert.Contains("src/sharing/routes.ts#postLink", view.GetProperty("callers").GetRawText(), StringComparison.Ordinal);
        Assert.Contains("src/sharing/links.ts#createLink", view.GetProperty("callees").GetRawText(), StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000), Trait("Lane", "integration")]
    public async Task An_index_at_another_commit_than_the_maps_reads_degraded_stale_with_both_commits()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var host = await Host(indexedAt: "1234567890abcdef1234567890abcdef12345678");

        var view = await Code(host);

        var row = view.GetProperty("searched").GetProperty("sources").EnumerateArray().Single(s => s.GetProperty("id").GetString() == "code-index");
        Assert.Equal(("degraded", "stale"), (row.GetProperty("state").GetString(), row.GetProperty("reason").GetString()));
        var callers = view.GetProperty("callers");
        Assert.Equal(("stale", true), (callers.GetProperty("state").GetString(), callers.GetProperty("asOfMismatch").GetBoolean()));
        Assert.Equal(_repo.Head, callers.GetProperty("asOfCommit").GetString());
    }

    private async Task<GoldenHost> Host(string indexedAt)
    {
        var create = new SymbolHit(_create, SymbolKind.Function, "createLink", "src/sharing/links.ts", 1, 9, "createLink()", Exported: true);
        var route = new SymbolHit(_route, SymbolKind.Function, "postLink", "src/sharing/routes.ts", 1, 9, "postLink()", Exported: true);
        var index = new FakeCodeIndex()
            .WithIndex(_repoName, CommitSha.From(indexedAt), create, route)
            .WithEdges(_repoName, _create, CallDirection.Callers, unresolved: 0, new CallEdge(1, route, "createLink", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 4, Candidates: 1, Coverage.Full))
            .WithEdges(_repoName, _route, CallDirection.Callees, unresolved: 0, new CallEdge(1, create, "createLink", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, Line: 4, Candidates: 1, Coverage.Full));
        var host = await ContextHosts.Approved(_repo.Path, sessions: true, TestContext.Current.CancellationToken, extra: services => _ = services.Replace(ServiceDescriptor.Singleton<ICodeIndex>(index)), name: CodeLensRepo.Repo);
        await ContextMapApiTests.Import(host, CodeLensRepo.WriteMap(host.Home, CodeLensRepo.MapYaml(_repo.Head)));
        return host;
    }

    private static async Task<JsonElement> Code(GoldenHost host)
    {
        var answer = await host.Http.Ask(Get("/sindri/context/code?anchor=feature%3Asharing", Human(host.Token.Value)));
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        return JsonDocument.Parse(answer.Body).RootElement;
    }
}
