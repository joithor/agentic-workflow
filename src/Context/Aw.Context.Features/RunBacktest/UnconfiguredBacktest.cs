using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Extract;
using Aw.Context.Core.Sources;
using Aw.Kernel;

namespace Aw.Context.Features.RunBacktest;

/// <summary>What a host that wires none of the backtest's ports gets: every call answers that nothing is set up (the way UnconfiguredHeavyJobLock does for builds).</summary>
internal sealed class UnconfiguredBacktest : IBriefSource, IMergeHistory, IAsOfDocs, IEvalStore
{
    private static Error Missing => ContextErrors.NoBriefSource.With("this host wires no backtest ports (fix: run it from Sindri)");

    public ValueTask<Result<BriefListing>> Issues(BriefScope scope, DateTimeOffset? since, CancellationToken cancellationToken) => ValueTask.FromResult<Result<BriefListing>>(Missing);

    public ValueTask<Result<Brief?>> Brief(BriefScope scope, BriefIssue issue, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Brief?>>(Missing);

    public ValueTask<Result<RepoHistory>> History(ContextRepo repo, CancellationToken cancellationToken) => ValueTask.FromResult<Result<RepoHistory>>(Missing);

    public ValueTask<Result<IContextSource>> Open(AsOfDocsRequest request, CancellationToken cancellationToken) => ValueTask.FromResult<Result<IContextSource>>(Missing);

    public ValueTask<Result<string>> WriteReport<T>(EvalReportKind kind, T report, DateTimeOffset at, CancellationToken cancellationToken)
        where T : class => ValueTask.FromResult<Result<string>>(Missing);

    public ValueTask<Result<ExtractedMap?>> ReadMap(string key, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ExtractedMap?>>(Missing);

    public ValueTask<Result<Unit>> WriteMap(string key, ExtractedMap map, CancellationToken cancellationToken) => ValueTask.FromResult<Result<Unit>>(Missing);

    public ValueTask<Result<EvalPurgeReport>> Purge(DateTimeOffset now, bool all, CancellationToken cancellationToken) => ValueTask.FromResult<Result<EvalPurgeReport>>(Missing);

    public ValueTask<Result<EvalPurgeReport>> Expired(DateTimeOffset now, CancellationToken cancellationToken) => ValueTask.FromResult<Result<EvalPurgeReport>>(Missing);
}
