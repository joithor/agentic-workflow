using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'recipe approve-all [--json]' (setup-once spec §5.3, §7.2): read the plan, print it (the server's challenge carries the plan; it is never
/// redeemed), and call the job route, which starts the job that approves every recipe in the plan. No typed phrase and no terminal are needed
/// (owner decision q93); an empty plan says so and exits 2.
/// </summary>
/// <param name="desk">The API client, the terminal input and the start rule.</param>
/// <param name="output">Where the verb writes.</param>
internal sealed class ApproveAllCommand(RecipeDesk desk, Output output)
{
    private const string Route = "/sindri/workflow/recipes/approve-all";

    internal async Task<int> Run(bool json, CancellationToken ct)
    {
        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var held = await ReadPlan(ct).ConfigureAwait(false);
        if (held.IsError)
        {
            return await output.Error(held.Error).ConfigureAwait(false);
        }

        var issued = await Issue(held.Value.Digest, ct).ConfigureAwait(false);
        if (issued.IsError)
        {
            return await output.Error(issued.Error).ConfigureAwait(false);
        }

        var challenge = issued.Value;
        var plan = challenge.ApproveAll!;   // why: Issue returns only a challenge that carries the plan
        var shown = ApproveAllText.Plan(plan);
        await (json ? output.OnStandardError().Out : output.Out).WriteAsync(shown).ConfigureAwait(false);
        var started = await desk.Client.Post<ChallengeAnswer, JobStartedResponse>(Route, body: null, ct).ConfigureAwait(false);
        return started.IsError
            ? await output.Error(started.Error).ConfigureAwait(false)
            : json ? await output.Raw(started.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(ApproveAllText.Started(started.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>The plan a hold would approve; an empty one is invalid input carrying the plan's own sentence.</summary>
    private async Task<Result<ApproveAllPlan>> ReadPlan(CancellationToken ct)
    {
        var read = await desk.Client.Get<ApproveAllPlan>(Route, ct).ConfigureAwait(false);
        return read.IsError ? read.Error
            : read.Value.Value.NothingToHold ? SindriErrors.InvalidInput.With(read.Value.Value.Empty ?? "There is nothing for one hold to approve (a recipe that needs its own approval is approved one at a time).")
            : read.Value.Value;
    }

    private async Task<Result<IssueChallengeResponse>> Issue(string digest, CancellationToken ct)
    {
        var issued = await desk.Client.Post<IssueChallengeRequest, IssueChallengeResponse>("/sindri/workflow/challenges",
            new IssueChallengeRequest(ChallengeAction.ApproveAll(digest).Text), ct).ConfigureAwait(false);
        return issued.IsError ? issued.Error
            : issued.Value.Value.ApproveAll is null ? SindriErrors.ApiNoCode.With("the challenge came back without an approve-all plan")
            : issued.Value.Value;
    }
}
