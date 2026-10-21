namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One labelled value of a step's Details. Its name keeps the visible words, so what is read is what is shown.</summary>
/// <param name="Label">The label ("Step").</param>
/// <param name="Value">The value as served.</param>
internal sealed record SetupFieldViewModel(string Label, string Value)
{
    /// <summary>The accessible name: "Step: map".</summary>
    public string Name => Label + ": " + Value;
}
