namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The Workflows page's state names (<see cref="WorkflowsViewModel"/>, continued): each recipe on the page is read once for the words of its states.</summary>
internal sealed partial class WorkflowsViewModel
{
    private readonly StateNames _names = new();

    // Reads the recipes the shown rows run that have not been asked for; rows then say their stage in the recipe's words. A recipe that cannot be read leaves its ids made readable.
    private async Task ReadNames(Uri url, CancellationToken cancellationToken)
    {
        var rows = _rows.Values.Concat(DoneRows).ToList();
        var changed = false;
        foreach (var recipe in rows.Select(r => r.Recipe).Distinct(StringComparer.Ordinal).Where(r => !_names.WasAsked(r)).ToList())
        {
            _names.Asked(recipe);
            var read = await api.Recipe(url, recipe, hash: null, cancellationToken).ConfigureAwait(true);
            changed |= read.IsSuccess && _names.Add(read.Value);
        }

        if (changed)
        {
            rows.ForEach(r => r.NamesChanged());
        }
    }
}
