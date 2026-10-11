using Aw.Evidence.Core.Judging;
using Microsoft.Extensions.Logging;

namespace Aw.Evidence.Infrastructure.Judging;

/// <summary>
/// The judge until its CLI adapter lands: every answer is null, so selector repair doesn't happen and the visual
/// verdict stays unchecked (callers fail closed). Says so once per process, at information.
/// </summary>
internal sealed partial class UnavailableJudge(ILogger<UnavailableJudge> logger) : IJudge
{
    private int _announced;

    /// <inheritdoc/>
    public ValueTask<SelectorRepair?> RepairSelectorAsync(SelectorRepairRequest request, CancellationToken cancellationToken)
    {
        Announce();
        return ValueTask.FromResult<SelectorRepair?>(null);
    }

    /// <inheritdoc/>
    public ValueTask<VisualVerdict?> CritiqueAsync(VisualCritiqueRequest request, CancellationToken cancellationToken)
    {
        Announce();
        return ValueTask.FromResult<VisualVerdict?>(null);
    }

    private void Announce()
    {
        if (Interlocked.Exchange(ref _announced, 1) == 0)
        {
            NotWired(logger);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The judge adapter is not wired yet: no selector repair, and visual verdicts stay unchecked")]
    private static partial void NotWired(ILogger logger);
}
