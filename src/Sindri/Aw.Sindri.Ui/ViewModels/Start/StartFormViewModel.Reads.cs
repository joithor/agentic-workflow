using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

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
        (Recipe, PickedItem, _chosen) = (null, null, null);
        (Recipes, Fields, Items, _itemKey) = ([], [], [], null);
        (_sentFirst, _sentNext, _blocked, _reading, OpenedFrame) = (false, false, false, false, false);
        (ItemId, Note, Outcome, IsSending, OffersNewAttempt) = (itemId ?? string.Empty, string.Empty, string.Empty, false, false);
        ClearError();
        Rest();
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
            return;   // Esc, Cancel or a new opening came first (each ends this form): this read is for a form that is gone
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

    /// <summary>Esc or Cancel: closes the form; nothing more is sent, and an answer still on its way opens no frame.</summary>
    internal void Close()
    {
        _form++;   // ends this form's reads and sends: their answers are for a form that is gone
        IsOpen = false;
    }

    /// <summary>Chooses a recipe and reads its inputs.</summary>
    /// <param name="recipe">The recipe.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that completes when the fields are shown.</returns>
    internal async Task Choose(RecipeChoice recipe, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        if (ReferenceEquals(_chosen, recipe))
        {
            return;   // the picker and a caller both say so: one read
        }

        var (form, choice) = (_form, ++_choice);
        _chosen = recipe;
        (Recipe, Fields, _itemKey, Note) = (recipe, [], null, string.Empty);
        ClearError();
        ResetAttempt();
        if (_url() is not { } api)
        {
            (_chosen, _reading, Recipe) = (null, false, null);   // no recipe is chosen: the same one can be picked again when the API is back
            StateChanged();
            Blocked(SindriUiErrors.NotRunning.With(SindriUiErrors.NotRunning.Title));
            return;
        }

        _reading = true;
        StateChanged();
        var read = await _api.Recipe(api, recipe.Id, hash: null, cancellationToken).ConfigureAwait(true);
        if (form != _form || choice != _choice)
        {
            return;   // another choice was made (even of this recipe again), or the form was opened again, while this one was read
        }

        _reading = false;
        StateChanged();
        if (read.IsError)
        {
            (_chosen, Recipe) = (null, null);   // no recipe is chosen: the picker raises a change when it is picked again
            Blocked(read.Error);
            return;
        }

        Show(read.Value);
    }

    private void Show(RecipeView read)
    {
        _blocked = false;
        _itemKey = read.Inputs.FirstOrDefault(i => i.Type == FactType.Item)?.Key;
        Fields = [.. read.Inputs.Where(i => i.Type != FactType.Item).Select(i => new StartFieldViewModel(i, _files))];
        foreach (var field in Fields)
        {
            field.Edited += (_, _) => ClearError();
        }

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
        StateChanged();
        Say(Readiness);
    }

    // Only an approved recipe starts; one row for each id, at the highest version (a start by id runs the latest approved).
    private static List<RecipeChoice> Approved(RecipeList list) =>
    [
        .. list.Recipes.Where(r => string.Equals(r.Standing, "approved", StringComparison.Ordinal))
            .GroupBy(r => r.Id, StringComparer.Ordinal)
            .Select(g => g.MaxBy(r => r.Version)!)   // why: a group has at least one row
            .Select(r => new RecipeChoice(r.Id, r.Version, Labels.StartRecipeLine(WorkflowNames.Of(r.Id, r.DisplayName)))),
    ];

    private static List<ItemChoice> Itemised(QueueView queue) =>
    [
        .. queue.Groups.SelectMany(g => g.Items).DistinctBy(i => i.Id, StringComparer.Ordinal).Select(i => new ItemChoice(i.Id, Labels.StartItemLine(i.Id, i.Title), i.Repo)),
    ];
}
