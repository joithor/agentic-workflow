using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.ApproveRecipe;
using Aw.Sindri.Features.IssueChallenge;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Features.RevokeRecipe;
using Aw.Sindri.Host.Commands.Recipe;
using Aw.Sindri.Host.Endpoints;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'recipe approve &lt;file|id@hash&gt;' and 'recipe revoke &lt;id@hash&gt;' (engine spec §4.2, §6.3; protected): ask the
/// server for a challenge, print its preview, read the action text the human types, and send it back. With a file the CLI
/// first checks it validates and that its eval files are present (SND-RECIPE-008), registers it if the server doesn't
/// know it, refuses before the preview when its id and version are registered with other content (SND-RECIPE-007), and
/// refuses with SND-RECIPE-009 if the file changed while the human read the preview.
/// </summary>
/// <param name="desk">The API client, the terminal input and the start rule.</param>
/// <param name="services">The scope the local validation resolves from.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class RecipeDeskCommands(RecipeDesk desk, IServiceProvider services, Output output)
{
    private const string Challenges = "/sindri/workflow/challenges";

    internal async Task<int> Approve(string target, CancellationToken ct)
    {
        var local = File.Exists(target) ? await LocalRecipe.Read(target, services, ct).ConfigureAwait(false) : null;
        if (local is { IsError: true })
        {
            return await output.Error(local.Error).ConfigureAwait(false);
        }

        return desk.Agent
            ? await ApproveAsAgent(target, local?.Value, ct).ConfigureAwait(false)
            : await Approve(target, local?.Value, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A session approves with no challenge (owner q93, 2026-10-23): it registers the file when the server doesn't know it and posts the
    /// approve with the file's eval files, which the server runs against the hash. Without a file, an 'id@hash' (the hash in full) is approved
    /// with no eval files, which the server accepts only for a recipe that lists none.
    /// </summary>
    private async Task<int> ApproveAsAgent(string target, LocalRecipe? file, CancellationToken ct)
    {
        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        if (file is null && target.Split('@') is not [{ Length: > 0 }, { Length: > 0 }])
        {
            return await output.Error(SindriErrors.InvalidInput.With("recipe approve needs a recipe file, or an id@hash with the hash in full")).ConfigureAwait(false);
        }

        if (file is not null)
        {
            var registered = await desk.Client.Post<RegisterRecipeRequest, RegisterRecipeResponse>("/sindri/workflow/recipes", new RegisterRecipeRequest(file.Text, file.File), ct).ConfigureAwait(false);
            if (registered.IsError)
            {
                return await output.Error(registered.Error).ConfigureAwait(false);
            }
        }

        var (id, hash) = file is null ? (RecipeId.From(target.Split('@')[0]), RecipeRoute.Hash(target.Split('@')[1])) : (file.Id, file.Hash);
        var approved = await desk.Client.Post<ApproveRecipeBody, ApproveRecipeResponse>(Route(id, hash, "approve"), new ApproveRecipeBody(Evals: file?.EvalFiles), ct).ConfigureAwait(false);
        return approved.IsError
            ? await output.Error(approved.Error).ConfigureAwait(false)
            : await output.Text(Approved(approved.Value.Value), 0).ConfigureAwait(false);
    }

    private async Task<int> Approve(string target, LocalRecipe? file, CancellationToken ct)
    {
        var issued = await Begin(file is null ? $"approve {target}" : string.Create(CultureInfo.InvariantCulture, $"approve {file.Id.Value} {file.Version}"), file, ct)
            .ConfigureAwait(false);
        if (issued.IsError || issued.Value.Approve is not { } preview)
        {
            return await output.Error(issued.IsError ? issued.Error : SindriErrors.ApiNoCode.With("the challenge came back without an approval preview")).ConfigureAwait(false);
        }

        if (await Unregistered(file, preview.Hash, ct).ConfigureAwait(false) is { } edited)
        {
            return await output.Error(edited).ConfigureAwait(false);
        }

        await output.Out.WriteAsync(RecipePreviewText.Approve(preview, file?.Evals)).ConfigureAwait(false);
        var typed = await Prompt(issued.Value.Action).ConfigureAwait(false);
        var changed = file is null ? null : await LocalRecipe.Changed(file, preview.Hash, services, ct).ConfigureAwait(false);
        if (changed is not null)
        {
            return await output.Error(changed).ConfigureAwait(false);
        }

        var approved = await desk.Client.Post<ChallengeAnswer, ApproveRecipeResponse>(Route(preview.Id, preview.Hash, "approve"),
            new ChallengeAnswer(issued.Value.Challenge, typed), ct).ConfigureAwait(false);
        return approved.IsError
            ? await output.Error(approved.Error).ConfigureAwait(false)
            : await output.Text(Approved(approved.Value.Value), 0).ConfigureAwait(false);
    }

    internal async Task<int> Revoke(string target, CancellationToken ct)
    {
        var issued = await Begin($"revoke {target}", file: null, ct).ConfigureAwait(false);
        if (issued.IsError || issued.Value.Revoke is not { } preview)
        {
            return await output.Error(issued.IsError ? issued.Error : SindriErrors.ApiNoCode.With("the challenge came back without a revoke preview")).ConfigureAwait(false);
        }

        await output.Out.WriteAsync(RecipePreviewText.Revoke(preview)).ConfigureAwait(false);
        var typed = await Prompt(issued.Value.Action).ConfigureAwait(false);
        var revoked = await desk.Client.Post<ChallengeAnswer, RevokeRecipeResponse>(Route(preview.Id, preview.Hash, "revoke"),
            new ChallengeAnswer(issued.Value.Challenge, typed), ct).ConfigureAwait(false);
        return revoked.IsError
            ? await output.Error(revoked.Error).ConfigureAwait(false)
            : await output.Text(Revoked(revoked.Value.Value), 0).ConfigureAwait(false);
    }

    /// <summary>Starts the control plane if this CLI may, then asks for the challenge; a file the server doesn't know is registered first.</summary>
    private async Task<Result<IssueChallengeResponse>> Begin(string action, LocalRecipe? file, CancellationToken ct)
    {
        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return down;
        }

        var issued = await Issue(action, ct).ConfigureAwait(false);
        if (file is null || issued.IsSuccess)
        {
            return issued;
        }

        // Registering the file names the real reason when its id and version hold other content (SND-RECIPE-007), even
        // where the server refused the registered content first (revoked); a file it doesn't know yet is
        // registered and asked for again.
        var registered = await desk.Client.Post<RegisterRecipeRequest, RegisterRecipeResponse>("/sindri/workflow/recipes",
            new RegisterRecipeRequest(file.Text, file.File), ct).ConfigureAwait(false);
        return registered.IsError && registered.Error.Code == WorkflowErrors.RecipeVersionTaken.Code ? registered.Error
            : issued.Error.Code != WorkflowErrors.RecipeNotFound.Code ? issued
            : registered.IsError ? registered.Error
            : await Issue(action, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Null unless the file's id and version are registered with other content (it was edited without raising its version): the
    /// register call names the real reason (SND-RECIPE-007 with its RCP-VERSION-001 line) before any preview or prompt.
    /// </summary>
    private async Task<Error?> Unregistered(LocalRecipe? file, RecipeHash registered, CancellationToken ct)
    {
        if (file is null || file.Hash == registered)
        {
            return null;
        }

        var refused = await desk.Client.Post<RegisterRecipeRequest, RegisterRecipeResponse>("/sindri/workflow/recipes",
            new RegisterRecipeRequest(file.Text, file.File), ct).ConfigureAwait(false);
        return refused.IsError ? refused.Error : WorkflowErrors.RecipeChallengeStale.With(string.Create(CultureInfo.InvariantCulture,
            $"{file.Path} hashes to {file.Hash.Display}, not {registered.Display} as registered for {file.Id.Value} version {file.Version}; run it again"));
    }

    private async Task<Result<IssueChallengeResponse>> Issue(string action, CancellationToken ct)
    {
        var answer = await desk.Client.Post<IssueChallengeRequest, IssueChallengeResponse>(Challenges, new IssueChallengeRequest(action), ct).ConfigureAwait(false);
        return answer.IsError ? answer.Error : answer.Value.Value;
    }

    private async Task<string> Prompt(string action)
    {
        await output.Out.WriteAsync($"Type '{action}' to confirm (the challenge lasts 5 minutes): ").ConfigureAwait(false);
        return (await desk.Input.ReadLineAsync().ConfigureAwait(false))?.Trim() ?? string.Empty;
    }

    private static string Route(RecipeId id, RecipeHash hash, string verb) => $"/sindri/workflow/recipes/{id.Value}@{hash.Digest}/{verb}";

    internal static string Approved(ApproveRecipeResponse r) => r.AlreadyApproved
        ? string.Create(CultureInfo.InvariantCulture, $"already approved {r.Approval.Id.Value}@{r.Approval.Hash.Display}; woke {r.Resumed.Count} open instances\n")
        : r.Resumed.Count == 0
            ? $"approved {r.Approval.Id.Value}@{r.Approval.Hash.Display}\n"
            : string.Create(CultureInfo.InvariantCulture, $"approved {r.Approval.Id.Value}@{r.Approval.Hash.Display}; woke {r.Resumed.Count} open instances (those parked on the drift resume)\n");

    private static string Revoked(RevokeRecipeResponse r) =>
        string.Create(CultureInfo.InvariantCulture, $"revoked {r.Revocation.Id.Value}@{r.Revocation.Hash.Display}; {r.Preview.Parks.Count} instances parked\n")
        + string.Concat(r.Cascade.Select(static c => $"  also revoked {c.Id.Value}@{c.Hash.Display} (generated from it)\n"));
}
