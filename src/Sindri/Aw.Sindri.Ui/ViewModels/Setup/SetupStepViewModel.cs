using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// One step of the setup list. It is kept by its id across reads and updated in place, so a control with focus keeps it. It maps served fields to words and decides nothing:
/// the ids and codes it holds are for the closed Details, never the words up front.
/// </summary>
internal sealed class SetupStepViewModel : ObservableObject
{
    private static readonly string[] _all =
    [
        nameof(Title), nameof(ChipText), nameof(StateWord), nameof(Why), nameof(DetailText), nameof(ProgressText), nameof(ErrorText), nameof(Items), nameof(Action), nameof(Fields), nameof(Name),
        nameof(HasDetail), nameof(HasProgress), nameof(HasError), nameof(HasItems), nameof(HasAction), nameof(ShowsActionOnCard), nameof(ShowsActionInList),
    ];

    private readonly Func<SetupStepView, SetupActionViewModel?, SetupActionViewModel?> _action;

    internal SetupStepViewModel(SetupStepView step, Func<SetupStepView, SetupActionViewModel?, SetupActionViewModel?> action)
    {
        (Step, _action) = (step, action);
        Derive();
    }

    /// <summary>The step's place in the checklist, from 1.</summary>
    public int Number { get; private set; }

    /// <summary>True while the row is open.</summary>
    public bool IsOpen { get; set; }

    /// <summary>The state word.</summary>
    public string ChipWord { get; private set; } = string.Empty;

    /// <summary>The state chip.</summary>
    public Aw.Sindri.Ui.Api.Workflow.StateChip Chip { get; private set; } = new(string.Empty, Aw.Sindri.Ui.Api.Workflow.ChipTone.Neutral);

    /// <summary>The line under the title.</summary>
    public string Sub { get; private set; } = string.Empty;

    /// <summary>What the app says about the step's own button (a retry in flight).</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>The fraction of a counted step, 0 to 1.</summary>
    public double ProgressFraction { get; private set; }

    /// <summary>The step as last served.</summary>
    internal SetupStepView Step { get; private set; }

    /// <summary>The step's id (repo, profile, recipes, mcp, map, onyx).</summary>
    public string Id => Step.Id;

    /// <summary>What the step is, in a few words.</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>The state: glyph and word ("◆ YOUR TURN").</summary>
    public string ChipText { get; private set; } = string.Empty;

    /// <summary>The state's word alone, in lower case.</summary>
    public string StateWord { get; private set; } = string.Empty;

    /// <summary>One sentence on why the step is in this state.</summary>
    public string Why { get; private set; } = string.Empty;

    /// <summary>What it did or is doing; empty when nothing.</summary>
    public string DetailText { get; private set; } = string.Empty;

    /// <summary>"40 of 100"; empty for an uncounted step.</summary>
    public string ProgressText { get; private set; } = string.Empty;

    /// <summary>Why it failed, in the API's sentence; empty otherwise.</summary>
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>One line per repo.</summary>
    public IReadOnlyList<SetupItemViewModel> Items { get; private set; } = [];

    /// <summary>The action, or null.</summary>
    public SetupActionViewModel? Action { get; private set; }

    /// <summary>The ids and codes, for the closed Details.</summary>
    public IReadOnlyList<SetupFieldViewModel> Fields { get; private set; } = [];

    /// <summary>The step's accessible name: title, state and why.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>True when it has detail text.</summary>
    public bool HasDetail => DetailText.Length > 0;

    /// <summary>True when it counts.</summary>
    public bool HasProgress => ProgressText.Length > 0;

    /// <summary>True when it failed with a sentence.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>True when it carries repo lines.</summary>
    public bool HasItems => Items.Count > 0;

    /// <summary>True when it has an action.</summary>
    public bool HasAction => Action is not null;

    /// <summary>True for the step the page leads with (the next one); its action is drawn there, once.</summary>
    public bool IsNext
    {
        get;
        internal set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(IsNext));
            OnChanged(nameof(ShowsActionOnCard));
            OnChanged(nameof(ShowsActionInList));
        }
    }

    /// <summary>True when the card draws the action on this row: only the next step carries it (one primary thing).</summary>
    public bool ShowsActionOnCard => IsNext && HasAction;

    /// <summary>True when the page's list draws the action on this row: every step but the one the page leads with.</summary>
    public bool ShowsActionInList => !IsNext && HasAction;

    /// <summary>True when the step needs the person.</summary>
    internal bool NeedsYou => string.Equals(Step.State, "needs-you", StringComparison.Ordinal);

    /// <summary>True when the step is done or skipped: setup is finished when every step is.</summary>
    internal bool IsFinished => string.Equals(Step.State, "done", StringComparison.Ordinal) || string.Equals(Step.State, "skipped", StringComparison.Ordinal);

    /// <summary>True while Sindri works on it.</summary>
    internal bool IsRunning => string.Equals(Step.State, "running", StringComparison.Ordinal);

    /// <summary>True when it stopped.</summary>
    internal bool IsFailed => string.Equals(Step.State, "failed", StringComparison.Ordinal);

    /// <summary>The card's priority: needs you, failed, running, then todo (and a state this build doesn't know), then done and skipped.</summary>
    internal int Rank => Step.State switch
    {
        "needs-you" => 0,
        "failed" => 1,
        "running" => 2,
        "done" or "skipped" => 4,
        _ => 3,
    };

    /// <summary>Takes a newer serving of the same step and redraws only what changed.</summary>
    /// <param name="step">The step as served now.</param>
    /// <returns>True when its state changed.</returns>
    internal bool Update(SetupStepView step)
    {
        var changed = !string.Equals(Step.State, step.State, StringComparison.Ordinal);
        Step = step;
        Derive();
        return changed;
    }

    /// <summary>Asks again whether the app can do its action now (the catalog or the Needs-you list changed).</summary>
    internal void Rederive()
    {
        Action = Step.Action is { Length: > 0 } ? _action(Step, Action) : null;
        foreach (var name in (string[])[nameof(Action), nameof(HasAction), nameof(ShowsActionOnCard), nameof(ShowsActionInList)])
        {
            OnChanged(name);
        }
    }

    private void Derive()
    {
        DeriveWords();
        DeriveParts();
        foreach (var name in _all)
        {
            OnChanged(name);
        }
    }

    private void DeriveWords()
    {
        var waiting = string.Equals(Step.State, "todo", StringComparison.Ordinal) && string.IsNullOrEmpty(Step.Action);
        var (glyph, word) = Labels.SetupChip(Step.State, waiting);
        (Title, Why, StateWord, ChipText) = (Step.Title, Step.Why, word.ToLowerInvariant(), glyph + " " + word);
        DetailText = Step.Detail ?? string.Empty;
        ProgressText = Step.Progress is { } progress ? Labels.SetupProgress(progress.Done, progress.Total) : string.Empty;
        ErrorText = Step.Error?.Sentence ?? string.Empty;
        Name = Labels.SetupStepName(Title, StateWord, Why);
    }

    private void DeriveParts()
    {
        Items = [.. (Step.Items ?? []).Select(i => new SetupItemViewModel(i.Repo, i.State, i.Detail))];
        Action = Step.Action is { Length: > 0 } ? _action(Step, Action) : null;
        Fields = Details();
    }

    private List<SetupFieldViewModel> Details()
    {
        List<SetupFieldViewModel> fields = [new(Labels.SetupStepField, Step.Id), new(Labels.SetupStateField, Step.State)];
        if (Step.Error is { } error)
        {
            fields.Add(new SetupFieldViewModel(Labels.SetupCodeField, error.Code));
        }

        if (Step.Action is { Length: > 0 } action)
        {
            fields.Add(new SetupFieldViewModel(Labels.SetupActionField, action));
        }

        return fields;
    }
}
