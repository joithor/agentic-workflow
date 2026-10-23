using Aw.Kernel;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>The Start form's routes of the fake (the recipe list and StartInstance): each answers a golden, the same text the handler serves.</summary>
internal sealed partial class FakeSindriApi
{
    private readonly List<StartInstanceRequest> _started = [];
    private readonly List<string> _reruns = [];
    private int _recipesCalls;

    /// <summary>GET /sindri/workflow/recipes' answer.</summary>
    public RouteAnswer RecipeListAnswer { get; set; } = RouteAnswer.Api("workflow-recipes", "list");

    /// <summary>POST /sindri/workflow/instances' answer.</summary>
    public RouteAnswer StartAnswer { get; set; } = RouteAnswer.Api("workflow-instances-start", "started");

    /// <summary>Held open until set: the recipe list waits for it.</summary>
    public TaskCompletionSource? RecipesGate { get; set; }

    /// <summary>Held open until set: a start waits for it (the form is sending meanwhile).</summary>
    public TaskCompletionSource? StartGate { get; set; }

    /// <summary>What a rerun answers: the same started view a start does.</summary>
    public RouteAnswer RerunAnswer { get; set; } = RouteAnswer.Api("workflow-instances-start", "started");

    /// <summary>The instances asked to run again, in order.</summary>
    public IReadOnlyList<string> Reruns { get { lock (_gate) { return [.. _reruns]; } } }

    /// <summary>Every start body received, in order (a start that never got there is not recorded).</summary>
    public IReadOnlyList<StartInstanceRequest> Started { get { lock (_gate) { return [.. _started]; } } }

    /// <summary>How many times the recipe list was read.</summary>
    public int RecipesCalls => Volatile.Read(ref _recipesCalls);

    public async Task<Result<RecipeList>> Recipes(Uri api, CancellationToken cancellationToken)
    {
        _ = Interlocked.Increment(ref _recipesCalls);
        await WaitFor(RecipesGate, cancellationToken).ConfigureAwait(false);
        return Answer<RecipeList>(api, WorkflowRoutes.Recipes, RecipeListAnswer);
    }

    public async Task<Result<StartedView>> StartInstance(Uri api, StartInstanceRequest request, CancellationToken cancellationToken)
    {
        if (NotReceived<StartedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _started.Add(request);
        }

        await WaitFor(StartGate, cancellationToken).ConfigureAwait(false);
        return Answer<StartedView>(api, WorkflowRoutes.Start, StartAnswer);
    }

    public async Task<Result<StartedView>> RerunInstance(Uri api, string instanceId, CancellationToken cancellationToken)
    {
        if (NotReceived<StartedView>(api) is { } unanswered)
        {
            return unanswered;
        }

        lock (_gate)
        {
            _reruns.Add(instanceId);
        }

        await WaitFor(StartGate, cancellationToken).ConfigureAwait(false);
        return Answer<StartedView>(api, WorkflowRoutes.Rerun(instanceId), RerunAnswer);
    }
}
