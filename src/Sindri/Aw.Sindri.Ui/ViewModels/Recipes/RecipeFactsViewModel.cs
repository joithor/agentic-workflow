using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>What the recipe declares as inputs and outputs, in declared order (screens spec §4.3).</summary>
internal sealed class RecipeFactsViewModel
{
    internal RecipeFactsViewModel(RecipeView recipe)
    {
        Inputs = [.. recipe.Inputs.Select(i => new RecipeFactRowViewModel(i.Label ?? WorkflowNames.InputName(i.Key), Labels.InputDetail(i), i.Generate is { Length: > 0 } template ? Labels.GenerateLine(template) : string.Empty))];
        Outputs = [.. recipe.Outputs.Select(o => new RecipeFactRowViewModel(o.Label ?? WorkflowNames.InputName(o.Key), Labels.OutputDetail(o), string.Empty))];
    }

    /// <summary>The inputs.</summary>
    public IReadOnlyList<RecipeFactRowViewModel> Inputs { get; }

    /// <summary>The outputs.</summary>
    public IReadOnlyList<RecipeFactRowViewModel> Outputs { get; }

    /// <summary>The technical lines the sheet keeps in its Details: each input's and output's key, type and kind, and an input's generate template.</summary>
    public IReadOnlyList<string> Details =>
        [.. Inputs.SelectMany(i => i.HasGenerate ? new[] { i.Detail, i.Generate } : [i.Detail]), .. Outputs.Select(o => o.Detail)];

    /// <summary>True when the recipe declares outputs.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>The inputs section's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string InputsHeading => Labels.InputsHeading;

    /// <summary>The outputs section's heading.</summary>
    public string OutputsHeading => Labels.OutputsHeading;

    /// <summary>What the outputs section says when there are none.</summary>
    public string NoOutputsText => Labels.NoOutputs;
#pragma warning restore CA1822, MA0041
}
