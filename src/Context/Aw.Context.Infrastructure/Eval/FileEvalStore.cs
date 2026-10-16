using System.Text.Json;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Store;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>Stub.</summary>
internal sealed class FileEvalStore(ContextStoreSettings settings, JsonSerializerOptions options) : IEvalStore
{
    public ValueTask<Result<string>> WriteReport<T>(EvalReportKind kind, T report, DateTimeOffset at, CancellationToken cancellationToken)
        where T : class
    {
        _ = (settings, options);
        throw new NotSupportedException();
    }

    public ValueTask<Result<ExtractedMap?>> ReadMap(string key, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<Unit>> WriteMap(string key, ExtractedMap map, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<EvalPurgeReport>> Purge(DateTimeOffset now, bool all, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<EvalPurgeReport>> Expired(DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();
}
