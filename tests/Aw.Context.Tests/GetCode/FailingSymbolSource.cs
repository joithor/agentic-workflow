using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Symbols;
using Aw.Kernel;

namespace Aw.Context.Tests.GetCode;

/// <summary>A code index whose reads fail (the block's file cannot be opened).</summary>
internal sealed class FailingSymbolSource : ICodeSymbolSource
{
    private static Result<T> Fail<T>() => ContextErrors.ExtractionUnreadable.With("the code index could not be read");

    public ValueTask<Result<CodeIndexHead>> Heads(ContextRepoName repo, string? asOf, CancellationToken cancellationToken) => ValueTask.FromResult(Fail<CodeIndexHead>());

    public ValueTask<Result<SymbolAnswer<SymbolHit>>> FindSymbols(ContextRepoName repo, SymbolQuery query, string? asOf, CancellationToken cancellationToken) => ValueTask.FromResult(Fail<SymbolAnswer<SymbolHit>>());

    public ValueTask<Result<SymbolAnswer<FileSymbolsRow>>> FileSymbols(ContextRepoName repo, IReadOnlyList<string> paths, string? asOf, CancellationToken cancellationToken) => ValueTask.FromResult(Fail<SymbolAnswer<FileSymbolsRow>>());

    public ValueTask<Result<CallAnswer>> Callers(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) => ValueTask.FromResult(Fail<CallAnswer>());

    public ValueTask<Result<CallAnswer>> Callees(ContextRepoName repo, string symbolRef, int depth, string? asOf, CancellationToken cancellationToken) => ValueTask.FromResult(Fail<CallAnswer>());
}
