using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Scheduling;

namespace Aw.Sindri.Tests.Server;

/// <summary>A background service named 'sour' whose every run returns an error (no throw): the ledger was unavailable.</summary>
internal sealed class FailingService : IScheduledService
{
    public static Error Failure { get; } = SindriErrors.LedgerUnavailable.With("sour: the ledger was busy");

    public ServiceName Identity() => ServiceName.From("sour");

    public ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ScheduledResult>>(Failure);
}
