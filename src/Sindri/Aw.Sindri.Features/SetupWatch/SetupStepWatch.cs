using Aw.Sindri.Core.Setup;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The 'setup' service's step watch (setup-once spec §6, §7.5): each tick it derives the six steps from their live inputs, the way GET /setup does, and records 'setup.step.changed' for each whose state differs from its last event.
/// It keeps no step state of its own beyond the ledger's events; a tick that can't record (no lease, an unreadable ledger) leaves the next tick to try again.
/// </summary>
/// <param name="facts">The sources.</param>
/// <param name="recorder">Appends the changes.</param>
/// <param name="logger">Where a source that threw, or a change that couldn't be recorded, is noted.</param>
internal sealed partial class SetupStepWatch(ISetupFacts facts, SetupStepRecorder recorder, ILogger<SetupStepWatch> logger)
{
    /// <summary>Derives the steps and records the changes.</summary>
    /// <param name="cancellationToken">Cancels the run.</param>
    /// <returns>A task that ends when the changes are recorded.</returns>
    internal async ValueTask Run(CancellationToken cancellationToken)
    {
        var steps = await new SetupDeriver(facts, (step, e) => LogSourceFailed(logger, step, e.GetType().Name)).Derive(cancellationToken).ConfigureAwait(false);
        var recorded = await recorder.Record(steps, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            LogNotRecorded(logger, recorded.Error.Code);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "setup: the {Step} source threw {ExceptionType}; that step is derived failed")]
    private static partial void LogSourceFailed(ILogger logger, string step, string exceptionType);

    [LoggerMessage(Level = LogLevel.Debug, Message = "setup: the step changes were not recorded ({Code}); the next tick tries again")]
    private static partial void LogNotRecorded(ILogger logger, string code);
}
