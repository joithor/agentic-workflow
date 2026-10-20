#pragma warning disable MA0041 // red-phase stub: members throw until the implementation commit
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>One step of the setup list. It is kept by its id across reads and updated in place, so a control with focus keeps it. It maps served fields to words and decides nothing.</summary>
internal sealed class SetupStepViewModel : ObservableObject
{
    internal SetupStepViewModel(SetupStepView step)
    {
        Step = step;
    }

    /// <summary>The step as last served.</summary>
    internal SetupStepView Step { get; private set; }

    /// <summary>The step's id (repo, profile, recipes, mcp, map, onyx).</summary>
    public string Id => Step.Id;

    /// <summary>What the step is, in a few words.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>The state: glyph and word.</summary>
    public string ChipText => throw new NotSupportedException();

    /// <summary>The state's word alone, in lower case.</summary>
    public string StateWord => throw new NotSupportedException();

    /// <summary>One sentence on why the step is in this state.</summary>
    public string Why => throw new NotSupportedException();

    /// <summary>What it did or is doing; empty when nothing.</summary>
    public string DetailText => throw new NotSupportedException();

    /// <summary>"40 of 100"; empty for an uncounted step.</summary>
    public string ProgressText => throw new NotSupportedException();

    /// <summary>Why it failed, in the API's sentence; empty otherwise.</summary>
    public string ErrorText => throw new NotSupportedException();

    /// <summary>One line per repo.</summary>
    public IReadOnlyList<SetupItemViewModel> Items => throw new NotSupportedException();

    /// <summary>The action, or null.</summary>
    public SetupActionViewModel? Action => throw new NotSupportedException();

    /// <summary>The ids and codes, for the closed Details.</summary>
    public IReadOnlyList<SetupFieldViewModel> Fields => throw new NotSupportedException();

    /// <summary>The step's accessible name.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>True when it has detail text.</summary>
    public bool HasDetail => throw new NotSupportedException();

    /// <summary>True when it counts.</summary>
    public bool HasProgress => throw new NotSupportedException();

    /// <summary>True when it failed with a sentence.</summary>
    public bool HasError => throw new NotSupportedException();

    /// <summary>True when it carries repo lines.</summary>
    public bool HasItems => throw new NotSupportedException();

    /// <summary>Takes a newer serving of the same step.</summary>
    /// <param name="step">The step as served now.</param>
    /// <returns>True when its state changed.</returns>
    internal bool Update(SetupStepView step)
    {
        Step = step;
        _ = (new SetupItemViewModel(), new SetupActionViewModel(), new SetupFieldViewModel(step.Id, step.State));
        throw new NotSupportedException();
    }

    /// <summary>True when it has an action.</summary>
    public bool HasAction => throw new NotSupportedException();
}
