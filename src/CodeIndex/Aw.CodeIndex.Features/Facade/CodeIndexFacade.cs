using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features.Facade;

/// <summary>The code index's facade over the index files (spec §7): the build and the reads that need only the structure layer.</summary>
public sealed class CodeIndexFacade : ICodeIndex
{
    private readonly object[] _ports;

    /// <summary>Creates the facade.</summary>
    /// <param name="build">The build slice.</param>
    /// <param name="store">The index files.</param>
    public CodeIndexFacade(IService<BuildCodeIndexRequest, BuildReport> build, IIndexStore store)
    {
        _ports = [build, store];
    }

    /// <inheritdoc />
    public ValueTask<Result<BuildReport>> Build(CodeIndexScope scope, RepoName repo, CommitSha commit, IProgress<string> progress, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<StatusAnswer>> Status(CodeIndexScope scope, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<RecordedHead>>> Heads(CodeIndexScope scope, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<LayerProbe>>> ProbeLayers(CodeIndexScope scope, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<FindSymbolsAnswer>> FindSymbols(CodeIndexScope scope, SymbolQuery query, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<FileSymbolsAnswer>> FileSymbols(CodeIndexScope scope, RepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<CallsAnswer>> Callers(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, bool includeImplementations, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<CallsAnswer>> Callees(CodeIndexScope scope, SymbolRef symbol, CallDepth depth, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<SimilarAnswer>> Similar(CodeIndexScope scope, SymbolRef symbol, CancellationToken cancellationToken) => throw NotBuilt();

    /// <inheritdoc />
    public ValueTask<Result<NeighborsAnswer>> Neighbors(CodeIndexScope scope, RepoName repo, string text, int k, CancellationToken cancellationToken) => throw NotBuilt();

    private NotSupportedException NotBuilt() => new($"The facade is not built yet ({_ports.Length} ports).");
}
