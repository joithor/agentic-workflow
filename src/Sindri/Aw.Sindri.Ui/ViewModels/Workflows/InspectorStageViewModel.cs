namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One stage row of the run drawer: its place, name, the step the run is on there, a time while it is the current stage, and its one status.</summary>
/// <param name="Index">The stage's place, from 1.</param>
/// <param name="Name">The stage's name.</param>
/// <param name="Detail">The step the run is on, for the current stage; empty otherwise.</param>
/// <param name="Time">How long the run has been in the stage, for the current stage; empty otherwise (finished stages serve no time yet).</param>
/// <param name="Status">The stage's status badge.</param>
/// <param name="IsCurrent">True for the stage the run is in.</param>
internal sealed record InspectorStageViewModel(int Index, string Name, string Detail, string Time, RunStatus Status, bool IsCurrent)
{
    /// <summary>The index as the circle draws it.</summary>
    public string IndexText => Index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>True when the stage has a step to name.</summary>
    public bool HasDetail => Detail.Length > 0;

    /// <summary>True when the stage has a time to show.</summary>
    public bool HasTime => Time.Length > 0;

    /// <summary>What a screen reader hears for the stage.</summary>
    public string AccessibleName => string.Join(", ", new[] { Name, Status.Word, Detail, Time }.Where(p => p.Length > 0));
}
