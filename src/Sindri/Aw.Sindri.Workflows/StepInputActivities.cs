using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Temporalio.Activities;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The Step inputs' activity (plan decision 1): 'FillInput' runs one input before the Step starts, keeps its scrubbed text
/// in memory only, and returns the metadata, which is all Temporal records.
/// </summary>
/// <param name="runner">Runs the input and shapes what it returned.</param>
/// <param name="texts">Where the text is kept.</param>
internal sealed class StepInputActivities(IStepInputRunner runner, IInjectionTexts texts)
{
    /// <summary>Runs the input and keeps its text.</summary>
    /// <param name="request">Which input, for which run.</param>
    /// <returns>The metadata of what it filled.</returns>
    [Activity("FillInput")]
    public async Task<FilledInput> FillInputAsync(FillInputRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cancel = ActivityExecutionContext.Current.CancellationToken;
        var context = new StepInputContext(request.Input, request.Step, EffectContexts.Arguments(request.With, request.Facts), EffectContexts.Elements(request.Facts),
            request.Repo is { } repo && RepoName.TryFrom(repo, out var name) ? name : null);
        Result<FilledOutput> filled;
        try
        {
            filled = await runner.Fill(context, request.Index, cancel).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException) // boundary: whatever an input throws; only its type goes into history, because its message could carry the text it filled
        {
            throw Failed(request, e);
        }

        if (filled.IsError)
        {
            // an input without a runtime or a plan that is invalid can't change by retrying; a throw from the input itself can
            throw new ApplicationFailureException(filled.Error.Message, errorType: filled.Error.Code, nonRetryable: true);
        }

        texts.Put(new InjectionKey(request.Address, request.State, request.Run, request.Input), filled.Value.Texts);
        return filled.Value.Metadata;
    }

    /// <summary>The failure an input's throw becomes: its type only, never its message or stack, which could carry the text it filled.</summary>
    private static ApplicationFailureException Failed(FillInputRequest request, Exception thrown) =>
        new($"input {request.Input.Value} failed", errorType: thrown.GetType().Name);
}
