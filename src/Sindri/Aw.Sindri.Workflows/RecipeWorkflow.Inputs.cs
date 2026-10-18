using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Common;
using Temporalio.Exceptions;
using Temporalio.Workflows;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The Step inputs of <see cref="RecipeWorkflow"/> (plan decisions 1 to 5, 8): before a desk Step opens, each input runs as a
/// 'FillInput' activity ('inputs:{token}:{n}'), in declaration order, once per run token. The workflow merges the metadata
/// the activities return, which is all history holds. An input whose runtime is gone parks the instance with the existing
/// 'no runtime' park (SND-WF-010, the question naming SND-WF-016); any other failure fails the Step.
/// </summary>
internal sealed partial class RecipeWorkflow
{
    private bool _fillsInputs;
    private string? _filled;
    private InjectionPlan _injection = InjectionPlan.Empty;

    /// <summary>What the inputs did before a run: nothing to report, a Step failure the loop decides, or a park.</summary>
    /// <param name="Failed">The failure trigger; the Step does not open.</param>
    /// <param name="Parked">The parked instance; the Step does not open.</param>
    private readonly record struct Filling(Trigger? Failed, WorkflowInstance? Parked);

    /// <summary>
    /// Runs the state's Step inputs once for this run token. A history recorded before the 'step-inputs-v1' patch fills nothing
    /// (it replays with the commands it had).
    /// </summary>
    private async Task<Filling> FillInputs(WorkflowInstance instance, OpenRun open, RecipeState state)
    {
        var fresh = !string.Equals(_filled, open.Token.Value, StringComparison.Ordinal);
        _filled = open.Token.Value;
        if (fresh)
        {
            _injection = InjectionPlan.Empty;   // the plan shown is the open run's
        }

        if (!fresh || !_fillsInputs || state.Step is not { Inputs.Count: > 0 } step)
        {
            return default;
        }

        List<FilledInput> filled = [];
        for (var n = 1; n <= step.Inputs.Count; n++)
        {
            var request = new FillInputRequest(instance.Address.Value, instance.Frame.State, open.Token.Value, n, step.Inputs[n - 1].Use, step.Use, step.Inputs[n - 1].With, instance.Facts, _repo);
            try
            {
                filled.Add(await Workflow.ExecuteActivityAsync((StepInputActivities a) => a.FillInputAsync(request), FillOptions(open, n, state)).ConfigureAwait(true));
            }
            catch (ActivityFailureException e) when (e.InnerException is ApplicationFailureException { ErrorType: var type } && string.Equals(type, WorkflowErrors.StepInputNoRuntime.Code, StringComparison.Ordinal))
            {
                return new Filling(Failed: null, await ParkNoRuntime(instance, open, step, request.Input).ConfigureAwait(true));
            }
            catch (ActivityFailureException e) when (e.InnerException is not CanceledFailureException)
            {
                var code = e.InnerException is ApplicationFailureException { ErrorType: { } t } ? t : "failed";   // never the message: it could carry text
                var reason = $"input {request.Input.Value} failed: {code}";
                return new Filling(new StepFailed("engine:inputs", open.Token, reason, Tokens: 0, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(reason)))), Parked: null);
            }
        }

        _injection = InjectionPlans.Merge(filled);
        return default;
    }

    private static ActivityOptions FillOptions(OpenRun open, int n, RecipeState state) => new()
    {
        ActivityId = string.Create(CultureInfo.InvariantCulture, $"inputs:{open.Token.Value}:{n}"),
        TaskQueue = StepTaskQueue.For(state, Workflow.Info.TaskQueue),
        StartToCloseTimeout = TimeSpan.FromSeconds(60),
        RetryPolicy = new RetryPolicy { MaximumAttempts = 3, InitialInterval = TimeSpan.FromSeconds(1), BackoffCoefficient = 2 },
    };

    /// <summary>Parks the instance for the input whose runtime is gone, mirrors it, and stops the Step from opening.</summary>
    private async Task<WorkflowInstance> ParkNoRuntime(WorkflowInstance before, OpenRun open, StepUse step, Core.Workflow.Ids.BlockId input)
    {
        var parked = Enter(InputParks.Park(before, input, step.Use, "engine:inputs"), "engine:inputs");
        var trigger = new StepFailed("engine:inputs", open.Token, InputParks.Question(input, step.Use), Tokens: 0, ReportSha256: string.Empty);   // why: the row's trigger; the park is not decided through it
        // why: a Step runs only after RunAsync set _start
        await Mirror(Transition(_start!, before, parked, trigger, open), NeedsYou(parked)).ConfigureAwait(true);
        return parked;
    }
}
