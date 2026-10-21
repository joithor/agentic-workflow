using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.Sindri.Host.CodeIndexHost;

/// <summary>
/// The facade until the index store is wired in (CI-9): every repo in scope has no index yet (spec §7.3), so the reads answer 'missing' and
/// the not-found SND-CODEIDX-013, the same words as a repo that was never built. CI-9 registers the real facade before it and this one yields.
/// </summary>
internal sealed class UnbuiltCodeIndex : ICodeIndex
{
    private static readonly Error _none = CodeIndexErrors.IndexNotBuilt.With(CodeIndexErrors.IndexNotBuilt.Title);

    public ValueTask<Result<BuildReport>> Build(CodeIndexScope scope, RepoName repo, CommitSha commit, IProgress<string> progress, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<BuildReport>>(CodeIndexErrors.IndexNotBuilt.With("the index builder is not wired yet"));

    public ValueTask<Result<StatusAnswer>> Status(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ValueTask.FromResult<Result<StatusAnswer>>(new StatusAnswer([.. scope.Repos.Select(static r => new RepoStatus(r.Name, IndexState.Missing, Commit: null, BuiltAt: null, [], Counts: null))]));
    }

    public ValueTask<Result<IReadOnlyList<RecordedHead>>> Heads(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ValueTask.FromResult<Result<IReadOnlyList<RecordedHead>>>(scope.Repos.Select(static r => new RecordedHead(r.Name, Commit: null)).ToList());
    }

    public ValueTask<Result<IReadOnlyList<LayerProbe>>> ProbeLayers(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return ValueTask.FromResult<Result<IReadOnlyList<LayerProbe>>>(scope.Repos.Select(static r => new LayerProbe(r.Name, [])).ToList());
    }

    public ValueTask<Result<FindSymbolsAnswer>> FindSymbols(CodeIndexScope scope, SymbolQuery query, CancellationToken cancellationToken) => None<FindSymbolsAnswer>();

    public ValueTask<Result<FileSymbolsAnswer>> FileSymbols(CodeIndexScope scope, RepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken) => None<FileSymbolsAnswer>();

    public ValueTask<Result<CallsAnswer>> Callers(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken) => None<CallsAnswer>();

    public ValueTask<Result<CallsAnswer>> Callees(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, CancellationToken cancellationToken) => None<CallsAnswer>();

    public ValueTask<Result<SimilarAnswer>> Similar(CodeIndexScope scope, SymbolRef symbol, CancellationToken cancellationToken) => None<SimilarAnswer>();

    public ValueTask<Result<NeighborsAnswer>> Neighbors(CodeIndexScope scope, RepoName repo, string text, int k, CancellationToken cancellationToken) => None<NeighborsAnswer>();

    private static ValueTask<Result<T>> None<T>() => ValueTask.FromResult<Result<T>>(_none);
}
