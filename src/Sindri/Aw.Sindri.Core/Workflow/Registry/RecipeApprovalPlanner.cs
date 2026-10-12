using System.Globalization;
using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Registry;

/// <summary>
/// Plans an approval (engine spec §4.1, §4.2, W8, W11): reads the registered recipe and its approvals, works out the pins
/// (each named block's descriptor hash, each child's latest approved hash) and the preview, and names the challenge's
/// action and subject. IssueChallenge, ApproveRecipe and ShowRecipe share it, so the preview a human reads is the one the
/// approval is checked against. A domain service over the registry ports; it writes nothing.
/// </summary>
/// <param name="store">The registered recipes.</param>
/// <param name="approvals">The approvals and revocations.</param>
/// <param name="wake">The running instances (a drift approval resumes its parked ones).</param>
/// <param name="catalog">This build's blocks.</param>
/// <param name="json">The package's AwJson options (they read the stored text and hash the pins).</param>
public sealed class RecipeApprovalPlanner(IRecipeStore store, IRecipeApprovals approvals, IRecipeWake wake, BlockCatalog catalog, JsonSerializerOptions json)
{
    /// <summary>The plan for 'approve &lt;id&gt; &lt;version&gt;': the version registered under that id.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="version">Its version.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or SND-RECIPE-002 when that version isn't registered, or the plan's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, int version, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        return versions.Value.FirstOrDefault(r => r.Version == version) is { } recipe
            ? await Approve(recipe, cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With(string.Create(CultureInfo.InvariantCulture,
                $"{id.Value} version {version} isn't registered; register it with sindri recipe register <file> (sindri recipe list shows what is)"));
    }

    /// <summary>The plan for 'approve &lt;id&gt;@&lt;hash&gt;', the hash given in full or by a prefix of 8 or more hex digits.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash or its prefix.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or SND-RECIPE-002 when no single registered version of the id matches, or the plan's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, string hash, CancellationToken cancellationToken)
    {
        var versions = await store.Versions(id, cancellationToken).ConfigureAwait(false);
        if (versions.IsError)
        {
            return versions.Error;
        }

        var matching = versions.Value.Where(r => string.Equals(r.Hash.Value, hash, StringComparison.Ordinal) || r.Hash.MatchesPrefix(hash)).ToList();
        return matching.Count == 1
            ? await Approve(matching[0], cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With(matching.Count == 0
                ? $"no registered {id.Value} has hash {hash}; sindri recipe list shows what is"
                : $"{hash} matches more than one {id.Value}; give more of the hash");
    }

    /// <summary>The plan for approving <paramref name="hash"/>, which must be registered under <paramref name="id"/>.</summary>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or SND-RECIPE-002, or the plan's refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RecipeId id, RecipeHash hash, CancellationToken cancellationToken)
    {
        var found = await store.Find(hash, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return found.Error;
        }

        return found.Value is { } recipe && recipe.Id == id
            ? await Approve(recipe, cancellationToken).ConfigureAwait(false)
            : WorkflowErrors.RecipeNotFound.With($"no recipe {id.Value}@{hash.Display} is registered; sindri recipe list shows what is");
    }

    /// <summary>
    /// The plan for approving <paramref name="recipe"/>. Refused when its hash is revoked (SND-RECIPE-005), it declares no
    /// evals (SND-RECIPE-008), a child it starts has no approved hash to pin (SND-RECIPE-003), or it is already approved
    /// with the same pins (SND-CLI-003, nothing to do).
    /// </summary>
    /// <param name="recipe">The registered recipe.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The plan, or the refusal.</returns>
    public async ValueTask<Result<ApprovalPlan>> Approve(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var standing = await Standing(recipe, cancellationToken).ConfigureAwait(false);
        if (standing.IsError)
        {
            return standing.Error;
        }

        var (parsed, current) = standing.Value;
        var pins = Pins(recipe, parsed, current);
        if (pins.IsError)
        {
            return pins.Error;
        }

        var latest = await approvals.Latest(recipe.Id, recipe.Version, recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        return latest.Value is { } before && before.Pins == pins.Value
            ? SindriErrors.InvalidInput.With($"{Name(recipe)} is already approved with these pins; nothing to approve (sindri recipe list shows it)")
            : await Plan(recipe, standing.Value, pins.Value, latest.Value, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<ApprovalPlan>> Plan(RegisteredRecipe recipe, (Recipe Parsed, IReadOnlyList<RecipeApproval> Current) standing, RecipePins pins,
        RecipeApproval? latest, CancellationToken cancellationToken)
    {
        var diff = await Diff(recipe, standing.Current, cancellationToken).ConfigureAwait(false);
        var resumes = latest is null ? Results.Success<IReadOnlyList<WorkflowInstanceId>>([]) : await wake.Running(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (diff.IsError || resumes.IsError)
        {
            return diff.IsError ? diff.Error : resumes.Error;
        }

        var preview = ApprovalPreviews.Approve(standing.Parsed, recipe.Hash, pins, catalog, latest) with { Diff = diff.Value, Resumes = resumes.Value };
        var subject = ChallengeSubject.ForRecipe(recipe.Hash, recipe.Version, latest is null ? null : PinRules.Digest(pins, json));
        return new ApprovalPlan(recipe, preview, latest, ChallengeAction.Approve(recipe.Id, recipe.Version), subject);
    }

    /// <summary>The recipe read back and the current approvals, unless the hash is revoked or the recipe declares no evals.</summary>
    private async ValueTask<Result<(Recipe Parsed, IReadOnlyList<RecipeApproval> Current)>> Standing(RegisteredRecipe recipe, CancellationToken cancellationToken)
    {
        var revoked = await approvals.Revocation(recipe.Hash, cancellationToken).ConfigureAwait(false);
        if (revoked.IsError || revoked.Value is not null)
        {
            return revoked.IsError ? revoked.Error : WorkflowErrors.RecipeRevoked.With($"{Name(recipe)} was revoked and can't be approved again; raise its version and approve that");
        }

        var parsed = AwJson.Deserialize<Recipe>(Encoding.UTF8.GetBytes(recipe.Text), json);
        if (parsed.IsError || parsed.Value.Evals.Count == 0)
        {
            return parsed.IsError ? parsed.Error : WorkflowErrors.RecipeEvalsFailing.With(
                $"{Name(recipe)} declares no evals; approving needs them (fix: write one with sindri recipe eval --scaffold <file>, list it under evals:, register again)");
        }

        var current = await approvals.Current(cancellationToken).ConfigureAwait(false);
        return current.IsError ? current.Error : (parsed.Value, current.Value);
    }

    /// <summary>The block pins from this build, and each child's latest approved hash (spec §4.1: a child is created from the hash its parent pinned).</summary>
    private Result<RecipePins> Pins(RegisteredRecipe recipe, Recipe parsed, IReadOnlyList<RecipeApproval> current)
    {
        var children = new List<ChildPin>();
        foreach (var child in parsed.States.Values.Select(static s => s.WaitFor?.Child?.Recipe).OfType<RecipeId>().Distinct().OrderBy(static c => c.Value, StringComparer.Ordinal))
        {
            if (current.Where(a => a.Id == child).MaxBy(static a => a.Version) is not { } approved)
            {
                return WorkflowErrors.RecipeNotApproved.With($"{Name(recipe)} starts child {child.Value}, which has no approved version to pin; approve {child.Value} first");
            }

            children.Add(new ChildPin(child, approved.Hash));
        }

        return new RecipePins(PinRules.Blocks(parsed, catalog, json), children);
    }

    /// <summary>The diff against the last approved version of the same id (another hash), or null when there is none.</summary>
    private async ValueTask<Result<string?>> Diff(RegisteredRecipe recipe, IReadOnlyList<RecipeApproval> current, CancellationToken cancellationToken)
    {
        if (current.Where(a => a.Id == recipe.Id && a.Hash != recipe.Hash).MaxBy(static a => a.Version) is not { } previous)
        {
            return Results.Success<string?>(value: null);
        }

        var before = await store.Find(previous.Hash, cancellationToken).ConfigureAwait(false);
        if (before.IsError || before.Value is not { } old)
        {
            return before.IsError ? before.Error : Results.Success<string?>(value: null);
        }

        return LineDiff.Unified(ApprovalPreviews.Pretty(old.Text), ApprovalPreviews.Pretty(recipe.Text),
            string.Create(CultureInfo.InvariantCulture, $"{Name(old)} (approved, version {old.Version})"),
            string.Create(CultureInfo.InvariantCulture, $"{Name(recipe)} (version {recipe.Version})"));
    }

    private static string Name(RegisteredRecipe recipe) => $"{recipe.Id.Value}@{recipe.Hash.Display}";
}
