namespace Aw.Sindri.Ui.ViewModels.Recipes;

/// <summary>One state of the recipe's band: its name, the link to the next, the loops that are notes on it, the outputs it produces and the states that branch off it.</summary>
internal sealed class RecipeStateNodeViewModel(string state)
{
    /// <summary>The state.</summary>
    public string State { get; } = state;

    /// <summary>The words screen readers hear: every visible line, then what the glyphs say.</summary>
    public string Name => State;

    /// <summary>What joins this state to the next: the step link, or the loop mark with the loop's name and cap; empty on the last.</summary>
    public string Link { get; init; } = string.Empty;

    /// <summary>The loops on this state that are not drawn between two neighbours.</summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    /// <summary>The outputs this state produces.</summary>
    public IReadOnlyList<string> Outputs { get; init; } = [];

    /// <summary>The states that branch off this one.</summary>
    public IReadOnlyList<RecipeStateNodeViewModel> Branches { get; init; } = [];

    /// <summary>True when there are notes.</summary>
    public bool HasNotes => Notes.Count > 0;

    /// <summary>True when there are outputs.</summary>
    public bool HasOutputs => Outputs.Count > 0;

    /// <summary>True when states branch off this one.</summary>
    public bool HasBranches => Branches.Count > 0;
}
