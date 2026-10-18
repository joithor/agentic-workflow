using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Start;

/// <summary>The Start form's reads (<see cref="StartFormViewModel"/>, continued): opening, the recipe list and a recipe's inputs.</summary>
internal sealed partial class StartFormViewModel
{
    /// <summary>Opens the form empty (or for one item): reads the recipes and the queue's items.</summary>
    /// <param name="itemId">The item to start for, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>A task that completes when both reads are shown.</returns>
    internal async Task Open(string? itemId, CancellationToken cancellationToken)
    {
        var form = ++_form;
        (Recipe, PickedItem) = (null, null);
        (Recipes, Fields, Items, _itemKey) = ([], [], [], null);
        (_sentFirst, _sentNext, _blocked) = (false, false, false);
        (ItemId, Note, Outcome, IsSending, OffersNewAttempt) = (itemId ?? string.Empty, string.Empty, string.Empty, false, false);
        ClearError();
        IsOpen = true;
        IsLoading = true;
        RefreshReadiness();
        if (_url() is not { } api)
        {
            Blocked(SindriUiErrors.NotRunning.With(SindriUiErrors.NotRunning.Title));
            IsLoading = false;
            return;
        }

        var recipes = _api.Recipes(api, cancellationToken);
        var queue = _api.Queue(api, cancellationToken);
        var (listed, queued) = (await recipes.ConfigureAwait(true), await queue.ConfigureAwait(true));
        if (form != _form)
        {
            return;   // Esc and a new opening came first: this read is for a form that is gone
        }

        IsLoading = false;
        Items = queued.IsSuccess ? Itemised(queued.Value) : [];   // the item can still be typed when the queue cannot be read
        if (listed.IsError)
        {
            Blocked(listed.Error);
            return;
        }

        Recipes = Approved(listed.Value);
        if (Recipes.Count == 0)
        {
            (_blocked, Note) = (true, Labels.StartNoRecipes);
            OnChanged(nameof(CanStart));
        }
    }

    /// <summary>Esc or Cancel: closes the form; nothing is sent.</summary>
    internal void Close() => IsOpen = false;

    /// <summary>Chooses a recipe and reads its inputs.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the fields are shown.</returns>
    internal async Task Choose(RecipeChoice recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        var form = _form;
        (Recipe, Fields, _itemKey, Note) = (recipe, [], null, string.Empty);
        ClearError();
        if (_url() is not { } api)
        {
            Blocked(SindriUiErrors.NotRunning.With(SindriUiErrors.NotRunning.Title));
            return;
        }

        var read = await _api.Recipe(api, recipe.Id, hash: null, cancellationToken).ConfigureAwait(true);
        if (form != _form || !ReferenceEquals(Recipe, recipe))
        {
            return;   // another recipe was chosen, or the form was opened again, while this one was read
        }

        if (read.IsError)
        {
            Blocked(read.Error);
            return;
        }

        _blocked = false;
        _itemKey = read.Value.Inputs.FirstOrDefault(i => i.Type == FactType.Item)?.Key;
        Fields = [.. read.Value.Inputs.Where(i => i.Type != FactType.Item).Select(i => new StartFieldViewModel(i, _files))];
        OnChanged(nameof(CanStart));
    }

    private void Blocked(Error error)
    {
        (_blocked, Note) = (true, error.Code + ": " + error.Message);
        OnChanged(nameof(CanStart));
    }

    private void RefreshReadiness()
    {
        OnChanged(nameof(IsReady));
        OnChanged(nameof(IsNotReady));
        OnChanged(nameof(Readiness));
        OnChanged(nameof(CanStart));
    }

    // Only an approved recipe starts; one row for each id, at the highest version (a start by id runs the latest approved).
    private static List<RecipeChoice> Approved(RecipeList list) =>
    [
        .. list.Recipes.Where(r => string.Equals(r.Standing, "approved", StringComparison.Ordinal))
            .GroupBy(r => r.Id, StringComparer.Ordinal)
            .Select(g => g.MaxBy(r => r.Version)!)   // why: a group has at least one row
            .Select(r => new RecipeChoice(r.Id, r.Version, Labels.StartRecipeLine(r.Id, r.Version))),
    ];

    private static List<ItemChoice> Itemised(QueueView queue) =>
    [
        .. queue.Groups.SelectMany(g => g.Items).DistinctBy(i => i.Id, StringComparer.Ordinal).Select(i => new ItemChoice(i.Id, Labels.StartItemLine(i.Id, i.Title))),
    ];
}
