using Aw.Context.Core.Engine;
using Aw.Kernel;

namespace Aw.Context.Core.Symbols;

/// <summary>The source Context registers until Sindri's Host replaces it with the code index adapter: every repo has no index, so every lens reads 'index-not-built' as it did before the port existed.</summary>
public sealed class NoCodeSymbolSource : ICodeSymbolSource
{
    private static readonly CodeIndexHead _none = CodeIndexHead.None;

    /// <inheritdoc/>
    public ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken) => ValueTask.FromResult<Result<CodeIndexHead>>(_none);

    /// <inheritdoc/>
    public ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SymbolAnswer<SymbolHit>>>(new SymbolAnswer<SymbolHit>(_none, []));

    /// <inheritdoc/>
    public ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<SymbolAnswer<FileSymbolsRow>>>(new SymbolAnswer<FileSymbolsRow>(_none, []));

    /// <inheritdoc/>
    public ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<CallAnswer>>(new CallAnswer(_none, [], 0));

    /// <inheritdoc/>
    public ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<CallAnswer>>(new CallAnswer(_none, [], 0));
}
