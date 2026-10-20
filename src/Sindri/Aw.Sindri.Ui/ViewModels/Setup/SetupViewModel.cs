using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The setup list behind the card on Home and the Setup page (setup-once spec §5.7): the six steps in words, the next one that needs the person, and the card's fold, which draws
/// three rows filled by priority (needs you, failed, running, todo, then done) and says how many it leaves. The list is read on connect and with every event, and the steps are
/// kept by id across reads. It decides nothing: states, sentences and actions are the API's; what the app adds is words, order and the fold.
/// </summary>
internal sealed partial class SetupViewModel : ObservableObject
{
    /// <summary>The rows the card draws (setup-once spec §5.7).</summary>
    internal const int CardRows = 3;

    private readonly Dictionary<string, SetupStepViewModel> _rows = new(StringComparer.Ordinal);
    private readonly ISindriApi _api;
    private readonly CatalogViewModel _catalog;
    private readonly TimeProvider _clock;
    private readonly Func<string, bool> _opensInApp;
    private int _reads;
    private bool _hasRead;
    private Uri? _url;

    /// <summary>The list over its ports.</summary>
    /// <param name="api">The API.</param>
    /// <param name="catalog">The command catalog: its rows give the terminal forms of the actions.</param>
    /// <param name="clock">The clock for the Copy controls' timings.</param>
    /// <param name="opensInApp">Whether the app can do an action itself (a button) rather than show the command; the shell decides.</param>
    internal SetupViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock, Func<string, bool> opensInApp)
    {
        (_api, _catalog, _clock, _opensInApp) = (api, catalog, clock, opensInApp);
        Launcher = new ControlViewModel(new CommandRowView("StartSindri", "desk", [], Terminal: false, Method: null, Route: null, Labels.SetupLauncher, "cli-only:launcher", Destructive: false, ReadsViews: false, Shortcut: null), send: null, catalog.Clipboard, clock);
    }

    /// <summary>Raised when a step's button asks the shell to open an action in the app.</summary>
    internal event EventHandler<SetupActionEventArgs>? ActionRequested;

    /// <summary>Raised when the card's link asks for the Setup page.</summary>
    internal event EventHandler? PageRequested;

    /// <summary>The steps in the order served.</summary>
    public ObservableCollection<SetupStepViewModel> Steps { get; } = [];

    /// <summary>The rows the card draws: three by priority, or every step once the person asks for them all.</summary>
    public ObservableCollection<SetupStepViewModel> CardSteps { get; } = [];

    /// <summary>The step the page leads with: the first that needs the person, else the first that failed, else the first Sindri is working on, else the first not started; null when setup is finished.</summary>
    public SetupStepViewModel? Next { get; private set => Set(ref field, value); }

    /// <summary>The ids of the steps the card draws; empty while it is not shown. Home hides a setup row for a step the card draws.</summary>
    internal IReadOnlySet<string> DrawnIds { get; private set; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The polite live region: a step changing is said once.</summary>
    public LiveAnnouncer Live { get; } = new(Labels.SetupPage);

    /// <summary>The launcher command with Copy, for when Sindri isn't running.</summary>
    public ControlViewModel Launcher { get; }

    /// <summary>"2 of 6 done"; empty until the first list.</summary>
    public string Head { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The page's lead: what the next step is to the person, or that setup is finished; empty until the first list.</summary>
    public string LeadText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"and 3 more" (and how many need you), or "Show fewer" once expanded.</summary>
    public string MoreText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The expand control's accessible name.</summary>
    public string MoreName { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the card leaves steps out (or shows them all after a press).</summary>
    public bool HasMore { get; private set => Set(ref field, value); }

    /// <summary>True once the person asked for every step on the card.</summary>
    public bool IsExpanded { get; private set => Set(ref field, value); }

    /// <summary>True when every step is done or skipped.</summary>
    public bool IsFinished { get; private set => Set(ref field, value); }

    /// <summary>True when the card has steps to draw: read, not finished, and a list this build can use.</summary>
    public bool IsCardVisible { get; private set => Set(ref field, value); }

    /// <summary>True when the card takes room on Home: its steps, or why there are none to show (Sindri isn't running, a failed read).</summary>
    public bool IsCardShown => IsCardVisible || IsNotRunning || IsFailed;

    /// <summary>True until the first answer.</summary>
    public bool IsReading { get; private set => Set(ref field, value); } = true;

    /// <summary>True when this Sindri has no setup list (an older one).</summary>
    public bool IsNotServed { get; private set => Set(ref field, value); }

    /// <summary>True when the answer can't be read by this build.</summary>
    public bool IsUnreadable { get; private set => Set(ref field, value); }

    /// <summary>True while the steps shown are the last good ones (a read failed).</summary>
    public bool IsStale { get; private set => Set(ref field, value); }

    /// <summary>True when the first read failed (Sindri is running) and there are no steps to show.</summary>
    public bool IsFailed { get; private set => Set(ref field, value); }

    /// <summary>The sentence for a failed first read.</summary>
    public string FailedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True when nothing answered and there are no steps to show.</summary>
    public bool IsNotRunning { get; private set => Set(ref field, value); }

    /// <summary>True once the list has steps.</summary>
    public bool HasSteps { get; private set => Set(ref field, value); }

    /// <summary>True when the page leads with a step.</summary>
    public bool HasNext => Next is not null;

    /// <summary>Shows every step on the card, or folds it back to three.</summary>
    internal void ToggleMore()
    {
        IsExpanded = !IsExpanded;
        Fold();
    }

    /// <summary>Asks the shell for the Setup page.</summary>
    internal void RequestOpenPage() => PageRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks again, for every step, whether the app can do its action now.</summary>
    internal void Rederive()
    {
        foreach (var row in _rows.Values)
        {
            row.Rederive();
        }
    }

    // The card's rows by priority (needs you, failed, running, todo, done; step order within each), the next step, and the words that go with them.
    private void Fold()
    {
        List<SetupStepViewModel> byPriority = [.. Steps.Select((s, i) => (Step: s, Order: i)).OrderBy(x => x.Step.Rank).ThenBy(x => x.Order).Select(x => x.Step)];
        IsFinished = Steps.Count > 0 && Steps.All(s => s.IsFinished);
        Next = IsFinished ? null : byPriority.FirstOrDefault(s => !s.IsFinished);
        foreach (var row in Steps)
        {
            row.IsNext = ReferenceEquals(row, Next);
        }

        HasSteps = Steps.Count > 0;
        var drawn = Drawn(byPriority);
        Patch.To(CardSteps, drawn);
        Phrase([.. byPriority.Except(drawn)]);
        Shown();
    }

    // Nothing once setup is finished; every step once the person asked for them; else the first three by priority.
    private List<SetupStepViewModel> Drawn(List<SetupStepViewModel> byPriority) => IsFinished ? [] : IsExpanded ? byPriority : [.. byPriority.Take(CardRows)];

    // The words that go with the fold: how many it leaves (and how many of those need the person), the head, and the page's lead.
    private void Phrase(List<SetupStepViewModel> hidden)
    {
        HasMore = !IsFinished && Steps.Count > CardRows;
        MoreText = IsExpanded ? Labels.SetupShowFewer : Labels.SetupMore(hidden.Count, hidden.Count(s => s.NeedsYou));
        MoreName = IsExpanded ? Labels.SetupShowFewer : Labels.SetupShowAll;
        Head = Steps.Count == 0 ? string.Empty : Labels.SetupHead(Steps.Count(s => s.IsFinished), Steps.Count);
        LeadText = Lead();
    }

    private string Lead() => Steps.Count == 0 ? string.Empty : IsFinished ? Labels.SetupFinished : Next switch
    {
        { NeedsYou: true } => Labels.SetupLeadYou,
        { IsFailed: true } => Labels.SetupLeadFailed,
        { IsRunning: true } => Labels.SetupLeadRunning,
        _ => Labels.SetupLeadTodo,
    };

    // Everything that follows from what is drawn: the card's visibility, and the ids Home hides rows for (announced only when the set changed).
    private void Shown()
    {
        IsCardVisible = !IsReading && !IsNotServed && !IsUnreadable && !IsFinished && Steps.Count > 0;
        var drawn = IsCardVisible ? new HashSet<string>(CardSteps.Select(s => s.Id), StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        if (!drawn.SetEquals(DrawnIds))
        {
            DrawnIds = drawn;
            OnChanged(nameof(DrawnIds));
        }

        OnChanged(nameof(IsCardShown));
        OnChanged(nameof(HasNext));
    }

    // A step's action: an app button when the shell says the app can do it, else the command to copy; none for an action nothing here knows. A kept action stays while nothing about it
    // changed, so a Copy in flight is not reset.
    private SetupActionViewModel? ActionOf(SetupStepView step, SetupActionViewModel? kept)
    {
        var action = step.Action!;   // why: only called for a step with an action
        var inApp = _opensInApp(action);
        if (kept is not null && kept.Matches(step, inApp))
        {
            return kept;
        }

        return Knows(action, inApp) ? new SetupActionViewModel(step, inApp, _catalog, _clock, a => ActionRequested?.Invoke(this, new SetupActionEventArgs(a))) : null;
    }

    private bool Knows(string action, bool inApp) => inApp || _catalog.Row(action) is not null || Labels.SetupCli(action).Length > 0;
}
