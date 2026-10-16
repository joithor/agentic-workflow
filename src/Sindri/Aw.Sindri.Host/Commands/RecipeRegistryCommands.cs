using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Features.ListRecipes;
using Aw.Sindri.Features.RegisterRecipe;
using Aw.Sindri.Host.Commands.Recipe;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// 'recipe register &lt;file&gt;' (desk), 'recipe show &lt;id@hash&gt; [--json]' and 'recipe list [--stale] [--json]' (queries) against
/// the running control plane (engine spec §6.1, §6.3). Register reads the file here and sends its text, so the server
/// never opens a client path; it starts the control plane first when this CLI may. The queries never start it.
/// </summary>
/// <param name="desk">The API client and the start rule.</param>
/// <param name="services">The scope the file reader resolves from.</param>
/// <param name="output">Where the verbs write.</param>
internal sealed class RecipeRegistryCommands(RecipeDesk desk, IServiceProvider services, Output output)
{
    private const string Recipes = "/sindri/workflow/recipes";

    internal async Task<int> Register(string file, CancellationToken ct)
    {
        var text = await services.GetRequiredService<IRecipeFiles>().Read(file, ct).ConfigureAwait(false);
        if (text.IsError)
        {
            return await output.Error(text.Error).ConfigureAwait(false);
        }

        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return await output.Error(down).ConfigureAwait(false);
        }

        var registered = await desk.Client.Post<RegisterRecipeRequest, RegisterRecipeResponse>(Recipes, new RegisterRecipeRequest(text.Value, Path.GetFileName(file)), ct)
            .ConfigureAwait(false);
        if (registered.IsError)
        {
            return await output.Error(registered.Error).ConfigureAwait(false);
        }

        var r = registered.Value.Value;
        return await output.Text($"{(r.Added ? "registered" : "already registered")} {r.Id.Value}@{r.Hash.Display} ({RecipeLineText.Word(r.Standing)})\n", 0).ConfigureAwait(false);
    }

    internal async Task<int> Show(string target, bool json, CancellationToken ct)
    {
        var at = target.IndexOf('@', StringComparison.Ordinal);
        if (at == 0 || at == target.Length - 1)
        {
            return await output.Error(SindriErrors.InvalidInput.With($"recipe show needs <id> or <id>@<hash>, for example workflow.step or workflow.step@9ab2d40c1e77 (got '{target}')")).ConfigureAwait(false);
        }

        var route = at < 0 ? $"{Recipes}/{Uri.EscapeDataString(target)}" : $"{Recipes}/{Uri.EscapeDataString(target[..at])}@{Uri.EscapeDataString(target[(at + 1)..])}";
        var shown = await desk.Client.Get<RecipeView>(route, ct).ConfigureAwait(false);
        if (shown.IsError)
        {
            return await output.Error(InstanceReads.NotRunning(shown.Error)).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(shown.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(RecipeLineText.Show(shown.Value.Value), 0).ConfigureAwait(false);
    }

    internal async Task<int> List(bool stale, bool json, CancellationToken ct)
    {
        var listed = await desk.Client.Get<ListRecipesResponse>(stale ? Recipes + "?stale=true" : Recipes, ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return await output.Error(InstanceReads.NotRunning(listed.Error)).ConfigureAwait(false);
        }

        return json
            ? await output.Raw(listed.Value.Body, 0).ConfigureAwait(false)
            : await output.Text(RecipeLineText.List(listed.Value.Value.Recipes, stale), 0).ConfigureAwait(false);
    }
}
