using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The confirmations (<see cref="NeedsYouConfirm"/>, continued): a changed workflow's typed approve (needs-you spec §8.3). The row names the recipe; the
/// version and hash come from GET /sindri/workflow/recipes, read again whenever a challenge is asked for. The challenge is 'approve &lt;recipe&gt; &lt;version&gt;',
/// its served text is what the box shows, and the answer goes to the recipe's own approve route with the hash read. Only a row whose subject is a recipe
/// is offered the box, and only when the catalog says ApproveRecipe is ready.
/// </summary>
internal sealed partial class NeedsYouConfirm
{
    private const string Standing = "approved";
    private const string GoneCode = "SND-RECIPE-002";   // the code the server gives a recipe it doesn't know

    // A recipe row with a recipe subject is typed; any other row with ApproveRecipe stays the terminal copy.
    private static NeedsYouConfirmKind RecipeKind(NeedsYouRowViewModel row) => IsRecipe(row) ? NeedsYouConfirmKind.Typed : NeedsYouConfirmKind.Terminal;

    private static bool IsRecipe(NeedsYouRowViewModel row) =>
        row.Entry.Subject is { Id.Length: > 0 } subject && string.Equals(subject.Kind, NeedsYouKinds.Recipe, StringComparison.Ordinal) && string.Equals(row.Entry.PrimaryAction, RecipeAction, StringComparison.Ordinal);

    /// <summary>The version a changed workflow's approval is for: the one still waiting for approval, else the newest.</summary>
    /// <param name="recipes">The registered recipes.</param>
    /// <param name="id">The recipe id.</param>
    /// <param name="hash">The hash the row's CLI line names, or null.</param>
    /// <returns>The version's row, or null when the recipe is not registered.</returns>
    internal static RecipeSummaryView? Pick(IReadOnlyList<RecipeSummaryView> recipes, string id, string? hash = null)
    {
        ArgumentNullException.ThrowIfNull(recipes);
        var of = recipes.Where(r => string.Equals(r.Id, id, StringComparison.Ordinal)).ToList();
        return (hash is null ? null : of.Find(r => string.Equals(r.Hash, hash, StringComparison.Ordinal)))
            ?? of.OrderBy(r => r.GainsCapability == true ? 0 : 1)
                .ThenBy(r => string.Equals(r.Standing, Standing, StringComparison.Ordinal) ? 1 : 0)
                .ThenByDescending(r => r.Version)
                .FirstOrDefault();
    }

    // The hash the row's CLI line names ('sindri recipe approve <id>@<hash>'), or null for a line that names none.
    private static string? CliHash(NeedsYouRowViewModel? row) =>
        row?.Entry.Cli is { } cli && cli.LastIndexOf('@') is >= 0 and var at ? cli[(at + 1)..] : null;

    // A refusal of a workflow's approval in words: the server's code and message, then the line that works in a terminal.
    private string RecipeFailure(string code, string message) =>
        $"{code}: {message} {Labels.NeedsYouRecipeTerminal} {_row?.Entry.Cli}".TrimEnd();

    // What the person reads of a refusal: an agent session is told the app is the person's; a workflow's failure also carries its code and the CLI line.
    private string Failure(Error error) =>
        !_recipeShown || string.Equals(error.Code, SessionRefusal, StringComparison.Ordinal) ? Words(error) : RecipeFailure(error.Code, error.Message);

    // The challenge of the shown row's kind.
    private Task Ask(int session) => _recipeShown ? AskRecipe(session) : AskProfile(session);

    // The answer of the shown row's kind, to its own route: the refusal, or whether it approved.
    private async Task<(Error? Refused, bool Approved)> Approve(Uri api, AppChallengeView challenge, string typed)
    {
        var answer = new ProfileAnswer(challenge.Challenge, typed);
        if (_recipeShown && _recipe is { } recipe)
        {
            var approved = await _api.ApproveRecipe(api, recipe.Id, recipe.Hash, answer, CancellationToken.None).ConfigureAwait(true);
            return (approved.IsError ? approved.Error : null, approved.IsSuccess);
        }

        var profile = await _api.ApproveProfile(api, new ApproveProfileRequest(_hash, answer), CancellationToken.None).ConfigureAwait(true);
        return (profile.IsError ? profile.Error : null, profile.IsSuccess && profile.Value.Approved);
    }

    // The version and hash as served now, then the challenge for 'approve <recipe> <version>'; its served text is what the box shows.
    private async Task AskRecipe(int session)
    {
        if (_url() is not { } api)
        {
            (_challenge, ProfileRefusal) = (null, RecipeFailure(SindriUiErrors.NotRunning.Code, Labels.NeedsYouRecipeNotRunning));
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        var listed = await _api.Recipes(api, CancellationToken.None).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        _recipe = listed.IsError ? null : Pick(listed.Value.Recipes, _hash, CliHash(_row));
        if (_recipe is not { } recipe)
        {
            (_challenge, ProfileRefusal) = (null, listed.IsError ? Failure(listed.Error) : RecipeFailure(GoneCode, Labels.NeedsYouRecipeGone));
            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        var action = "approve " + recipe.Id + " " + recipe.Version.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var asked = await _api.IssueAppChallenge(api, new AppChallengeRequest(action), CancellationToken.None).ConfigureAwait(true);
        if (session != _session)
        {
            return;
        }

        if (asked.IsError)
        {
            (_challenge, ProfileRefusal) = (null, Failure(asked.Error));
        }
        else
        {
            _challenge = asked.Value;
            TypedBox.Offer(asked.Value);
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
