using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.GetInstance;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>An instance's workflow events, fixed; remembers which instance it was asked for.</summary>
internal sealed class StepRunEvents(params RecordedWorkflowEvent[] events) : IStepRunSource
{
    private Error? _failure;

    /// <summary>The instance the last read asked for.</summary>
    public InstanceAddress? Asked { get; private set; }

    /// <summary>A source whose ledger can't be read.</summary>
    public static StepRunEvents Failing() => new() { _failure = SindriErrors.LedgerUnavailable.With("ledger.db: database or disk is full") };

    public ValueTask<Result<IReadOnlyList<RecordedWorkflowEvent>>> EventsOf(InstanceAddress address, CancellationToken cancellationToken)
    {
        Asked = address;
        return ValueTask.FromResult(_failure is { } failure ? Results.Failure<IReadOnlyList<RecordedWorkflowEvent>>(failure) : Results.Success<IReadOnlyList<RecordedWorkflowEvent>>(events));
    }
}
