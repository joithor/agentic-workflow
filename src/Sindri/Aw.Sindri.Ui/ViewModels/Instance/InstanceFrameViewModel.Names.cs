using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The frame's state names (<see cref="InstanceFrameViewModel"/>, continued): the recipe is read once for the words of its states, and the band and the facts then say them instead of the ids.</summary>
internal sealed partial class InstanceFrameViewModel
{
    private readonly StateNames _names = new();
    private InstanceDetail? _shown;

    // A state in the recipe's words once the recipe is read, else its id as served.
    private string NameOf(string state) => _names.Served(Recipe, state) ?? state;

    // The header's state in the recipe's words, and the identity line that carries it.
    private void ShowIdentity(InstanceDetail detail)
    {
        StateText = NameOf(detail.Current.Status.Header.State);
        Identity = $"{WorkflowName} · {StateText}{(HasDetail ? ": " + Detail : string.Empty)} · {SinceText}";
    }

    // Reads the recipe the shown instance runs, once; when it serves names, the parts that say a state are drawn again with them. A recipe that cannot be read leaves the ids.
    private async Task ReadNames(Uri url, InstanceDetail detail, int generation, CancellationToken cancellationToken)
    {
        var recipe = detail.Recipe.Id;
        if (_names.WasAsked(recipe))
        {
            return;
        }

        _names.Asked(recipe);
        var read = await api.Recipe(url, recipe, hash: null, cancellationToken).ConfigureAwait(true);
        if (generation == _generation && read.IsSuccess && _names.Add(read.Value) && _shown is { } shown)
        {
            ShowIdentity(shown);
            ShowParts(shown);
        }
    }
}
