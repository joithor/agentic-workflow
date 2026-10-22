using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;

namespace Aw.CodeIndex.Testing;

/// <summary>
/// The IIndexStore contract: the same tests run against <see cref="FakeIndexStore"/> and the SQLite store
/// (.agents/rules/csharp.md, Seams). A derived class supplies a fresh store with nothing built and a way to damage the
/// live index.
/// </summary>
public abstract partial class IndexStoreContract
{
    /// <summary>The repo every contract test builds.</summary>
    protected static readonly RepoName Repo = RepoName.From("fixture");

    /// <summary>The commit every contract test publishes unless it says otherwise.</summary>
    protected static readonly CommitSha Commit = CommitSha.From("0123456789abcdef0123456789abcdef01234567");

    /// <summary>The build time every contract test publishes.</summary>
    protected static readonly DateTimeOffset BuiltAt = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static readonly CancellationToken _none = CancellationToken.None;

    /// <summary>A fresh store with no index for any repo.</summary>
    /// <returns>The store.</returns>
    protected abstract IIndexStore Create();

    /// <summary>Damages the repo's live index so that it can no longer be read.</summary>
    /// <param name="store">A store made by <see cref="Create"/>.</param>
    /// <param name="repo">The repo.</param>
    protected abstract void Corrupt(IIndexStore store, RepoName repo);

    /// <summary>The header every contract test publishes.</summary>
    /// <param name="commit">The commit, or <see cref="Commit"/>.</param>
    /// <returns>The meta; its counts are the store's to fill in.</returns>
    protected static IndexMeta Meta(CommitSha? commit = null)
        => new(commit ?? Commit, "main", BuiltAt, CodeIndexRules.SchemaVersion, "parser-1", LockSha: "lock-1", new IndexCounts(0, 0, 0));

    /// <summary>A symbol with deterministic defaults: the name is the last segment of the qualified name.</summary>
    /// <param name="file">The file that holds it.</param>
    /// <param name="qualified">Its qualified name.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="arity">Its parameter count.</param>
    /// <param name="line">Its first line.</param>
    /// <returns>The record.</returns>
    protected static SymbolRecord SymbolOf(string file, string qualified, SymbolKind kind = SymbolKind.Method, int arity = 0, int line = 1)
    {
        var name = qualified[(qualified.LastIndexOf('.') + 1)..];
        return new SymbolRecord(SymbolRef.From($"{Repo.Value}:{file}#{qualified}"), name, qualified, kind, arity, line, line + 4, Exported: true, Utility: false, $"void {name}()", $"shape-{qualified}", TokenCount: 10, Complexity: 1, MinHash: default, [], Body: null, []);
    }

    /// <summary>A parsed C# file holding the symbols.</summary>
    /// <param name="path">The path.</param>
    /// <param name="symbols">What it defines.</param>
    /// <returns>The record, with the blob id 'blob-&lt;path&gt;'.</returns>
    protected static FileRecord FileOf(string path, params SymbolRecord[] symbols) => new(path, $"blob-{path}", 10, "csharp", Parsed: true, symbols);

    /// <summary>A resolved, certain syntax call edge.</summary>
    /// <param name="from">The caller.</param>
    /// <param name="to">The callee.</param>
    /// <param name="line">The call's line.</param>
    /// <returns>The edge.</returns>
    protected static EdgeRecord CallEdge(SymbolRecord from, SymbolRecord to, int line = 3)
        => new(from.Ref, to.Ref, to.Name, EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Certain, line, Candidates: 1);

    /// <summary>A call to a name that matched no symbol.</summary>
    /// <param name="from">The caller.</param>
    /// <param name="name">The name.</param>
    /// <param name="line">The call's line.</param>
    /// <returns>The edge, with no target.</returns>
    protected static EdgeRecord UnresolvedEdge(SymbolRecord from, string name, int line = 3)
        => new(from.Ref, Target: null, name, EdgeKind.Calls, EdgeBasis.Syntax, Confidence.Unresolved, line, Candidates: 0);

    /// <summary>Builds and publishes an index from scratch.</summary>
    /// <param name="store">The store.</param>
    /// <param name="files">The files to write.</param>
    /// <param name="edges">The edges to add after the files.</param>
    /// <returns>The published header.</returns>
    protected static async Task<IndexHeader> Publish(IIndexStore store, FileRecord[] files, params EdgeRecord[] edges)
    {
        var build = await Begin(store, incremental: false);
        await using (build)
        {
            Succeeds(await build.WriteFiles(files, _none), "files are written");
            Succeeds(await build.AddEdges(edges, _none), "edges are added");
            return Ok(await build.Publish(Meta(), _none), "the build publishes");
        }
    }

    /// <summary>Starts a build.</summary>
    /// <param name="store">The store.</param>
    /// <param name="incremental">Whether to copy the live index.</param>
    /// <returns>The build.</returns>
    protected static async Task<IIndexBuild> Begin(IIndexStore store, bool incremental)
        => Ok(await store.BeginBuild(Repo, incremental, _none), "a build starts");

    /// <summary>Opens the live index.</summary>
    /// <param name="store">The store.</param>
    /// <returns>The reader.</returns>
    protected static async Task<IIndexReader> Reader(IIndexStore store)
        => Ok(await store.OpenRead(Repo, _none), "the live index opens");

    /// <summary>Unwraps a success; a failure breaks the contract.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="what">What was expected to succeed.</param>
    /// <returns>The value.</returns>
    protected static T Ok<T>(Kernel.Result<T> result, string what)
    {
        Expect.That(result.IsSuccess, $"{what} (got {(result.IsError ? result.Error.Code : "success")})");
        return result.Value;
    }

    /// <summary>Checks that a result succeeded.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="what">What was expected to succeed.</param>
    protected static void Succeeds<T>(Kernel.Result<T> result, string what) => _ = Ok(result, what);

    /// <summary>Checks that a result failed with a code.</summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="code">The expected catalogue code.</param>
    /// <param name="what">What was expected to fail.</param>
    protected static void Fails<T>(Kernel.Result<T> result, string code, string what)
        => Expect.That(result.IsError && string.Equals(result.Error.Code, code, StringComparison.Ordinal), $"{what} fails with {code} (got {(result.IsError ? result.Error.Code : "success")})");

    /// <summary>The one item of a list.</summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The list.</param>
    /// <returns>The item; a list of any other length breaks the contract.</returns>
    protected static T Only<T>(IEnumerable<T> items)
    {
        var all = items.ToList();
        Expect.That(all.Count == 1, $"exactly one item is expected (got {all.Count})");
        return all[0];
    }

    /// <summary>The refs of hits, in order.</summary>
    /// <param name="hits">The hits.</param>
    /// <returns>The ref texts.</returns>
    protected static string[] Refs(IEnumerable<Core.Facade.SymbolHit> hits) => [.. hits.Select(h => h.Ref.Value)];
}
