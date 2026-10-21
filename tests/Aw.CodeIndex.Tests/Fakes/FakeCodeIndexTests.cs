using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.Fakes;

/// <summary>The scripted facade keeps the scope rule the surfaces rely on (spec §7.3): another repo answers the same not-found as one never built.</summary>
public sealed class FakeCodeIndexTests
{
    private static readonly RepoName _api = RepoName.From("api");
    private static readonly RepoName _web = RepoName.From("web");
    private static readonly CommitSha _commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");
    private static readonly SymbolRef _run = SymbolRef.From("api:src/A.cs#A.Run");
    private static readonly SymbolRef _stop = SymbolRef.From("web:src/B.cs#B.Stop");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CodeIndexScope Scope(CallerKind kind, params RepoName[] repos)
        => new([.. repos.Select(r => new CodeIndexRepo(r, "/work/" + r.Value, CodeIndexSettings.Default))], new CodeIndexCaller(kind, kind == CallerKind.Session ? repos[0] : null));

    private static SymbolHit Hit(SymbolRef symbol, string file, string name) => new(symbol, SymbolKind.Method, name, file, 3, 9, "void " + name + "()", Exported: true);

    private static FakeCodeIndex Fake() => new FakeCodeIndex()
        .WithIndex(_api, _commit, Hit(_run, "src/A.cs", "Run"))
        .WithIndex(_web, _commit, Hit(_stop, "src/B.cs", "Stop"));

    [Fact, Trait("Lane", "unit")]
    public async Task Status_lists_only_the_repos_in_the_scope_and_marks_one_with_no_index_missing()
    {
        var status = await Fake().Status(Scope(CallerKind.Session, _web, RepoName.From("other")), Token);

        Assert.Equal([(_web, IndexState.Ok), (RepoName.From("other"), IndexState.Missing)], [.. status.Value.Repos.Select(r => (r.Repo, r.State))]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_symbol_in_a_repo_outside_the_scope_is_the_not_built_not_found_not_a_leak()
    {
        var answer = await Fake().Callers(Scope(CallerKind.Session, _web), _run, CallDepth.One, includeImplementations: false, Token);

        Assert.Equal("SND-CODEIDX-013", answer.Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_ref_that_names_no_symbol_is_011_and_an_empty_query_is_012()
    {
        var fake = Fake();
        var scope = Scope(CallerKind.Human, _api, _web);

        var unknown = await fake.Callees(scope, SymbolRef.From("api:src/A.cs#A.Nope"), CallDepth.One, Token);
        var empty = await fake.FindSymbols(scope, new SymbolQuery(" ", SymbolMatch.Exact, Repo: null), Token);

        Assert.Equal(("SND-CODEIDX-011", "SND-CODEIDX-012"), (unknown.Error.Code, empty.Error.Code));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Symbol_search_matches_exact_by_name_path_hash_name_or_ref_and_by_prefix_inside_the_scope_only()
    {
        var fake = Fake();
        var both = Scope(CallerKind.Human, _api, _web);

        var exact = await fake.FindSymbols(both, new SymbolQuery("src/A.cs#Run", SymbolMatch.Exact, Repo: null), Token);
        var prefix = await fake.FindSymbols(both, new SymbolQuery("S", SymbolMatch.Prefix, Repo: null), Token);
        var outside = await fake.FindSymbols(Scope(CallerKind.Session, _api), new SymbolQuery("Stop", SymbolMatch.Exact, Repo: null), Token);

        Assert.Equal(["Run"], [.. exact.Value.Hits.Select(h => h.Name)]);
        Assert.Equal(["Stop"], [.. prefix.Value.Hits.Select(h => h.Name)]);
        Assert.Empty(outside.Value.Hits);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Scripted_edges_come_back_with_their_unresolved_count_and_a_damaged_index_reads_as_not_built()
    {
        var edge = new CallEdge(1, Other: null, "Helper", EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Unresolved, Line: 5, Candidates: 0, Coverage.Full);
        var fake = Fake().WithEdges(_api, _run, CallDirection.Callees, unresolved: 2, edge).WithDamaged(_web);

        var callees = await fake.Callees(Scope(CallerKind.Human, _api, _web), _run, CallDepth.One, Token);
        var damaged = await fake.Callers(Scope(CallerKind.Human, _api, _web), _stop, CallDepth.One, includeImplementations: false, Token);

        Assert.Equal((2, 1), (callees.Value.UnresolvedCount, callees.Value.Edges.Count));
        Assert.Equal("SND-CODEIDX-013", damaged.Error.Code);
    }

    private static CallEdge Edge(Coverage coverage) => new(1, Other: null, "Go", EdgeKind.Calls, EdgeBasis.Graphify, Confidence.Likely, Line: 4, Candidates: 0, coverage);

    [Fact, Trait("Lane", "unit")]
    public async Task A_csharp_answer_with_full_rows_is_full_and_one_partial_row_makes_it_partial()
    {
        var scope = Scope(CallerKind.Human, _api);
        var full = await Fake().WithEdges(_api, _run, CallDirection.Callers, unresolved: 0, Edge(Coverage.Full)).Callers(scope, _run, CallDepth.One, includeImplementations: false, Token);
        var mixed = await Fake().WithEdges(_api, _run, CallDirection.Callers, unresolved: 0, Edge(Coverage.Full), Edge(Coverage.Partial)).Callers(scope, _run, CallDepth.One, includeImplementations: false, Token);

        Assert.Equal((Coverage.Full, Coverage.Partial), (full.Value.Coverage, mixed.Value.Coverage));
        Assert.Equal([Coverage.Full, Coverage.Partial], [.. mixed.Value.Edges.Select(e => e.Coverage)]);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_empty_answer_for_a_graphify_language_is_partial_so_it_reads_none_found()
    {
        var go = SymbolRef.From("web:src/tabs.ts#go");
        var fake = new FakeCodeIndex().WithIndex(_web, _commit, Hit(go, "src/tabs.ts", "go"));

        var answer = await fake.Callees(Scope(CallerKind.Human, _web), go, CallDepth.One, Token);

        Assert.Equal((Coverage.Partial, 0), (answer.Value.Coverage, answer.Value.Edges.Count));
    }
}
