using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>
/// The setup checklist, the first section of Settings, and Home's one line while any step is open (setup spec §2.4): the six steps in the app's own words, the progress strip, and
/// which rows are open. The list is read on connect and with every event, and the steps are kept by id across reads. It decides nothing: states, sentences and actions are the
/// API's; what the app adds is words, order, and the one button the map shows when it failed.
/// </summary>
internal sealed partial class SetupViewModel : ObservableObject
{
    private const string MapStep = "map";
    private const string MapRefresh = "RefreshContextMap";
    private const string RunningRefusal = "SND-JOB-409";

    private readonly Dictionary<string, SetupStepViewModel> _rows = new(StringComparer.Ordinal);
    private readonly ISindriApi _api;
    private readonly CatalogViewModel _catalog;
    private readonly TimeProvider _clock;
    private readonly Func<string, bool> _opensInApp;
    private int _reads;
    private bool _hasRead;
    private bool _refreshing;
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

    /// <summary>Raised when Home's line asks for the checklist.</summary>
    internal event EventHandler? ChecklistRequested;

    /// <summary>Raised when the checklist should come into view (Settings was opened by a link, G U, ⌘, or the line).</summary>
    internal event EventHandler<SetupRevealEventArgs>? RevealRequested;

    /// <summary>The steps in the order served.</summary>
    public ObservableCollection<SetupStepViewModel> Steps { get; } = [];

    /// <summary>The polite live region: a step changing is said once.</summary>
    public LiveAnnouncer Live { get; } = new(Labels.Settings);

    /// <summary>The launcher command with Copy, for when Sindri isn't running.</summary>
    public ControlViewModel Launcher { get; }

    /// <summary>The lead over the checklist: "Set up Sindri", or "Sindri is set up"; empty until the first list.</summary>
    public string Lead { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>"2 of 6 done", or "All 6 done"; empty until the first list.</summary>
    public string CountText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the one-line sub shows (hidden once setup is finished).</summary>
    public bool HasSub { get; private set => Set(ref field, value); }

    /// <summary>One segment per step: done, working or still to do.</summary>
    public IReadOnlyList<SetupSegment> Segments { get; private set => Set(ref field, value); } = [];

    /// <summary>True when every step is done or skipped.</summary>
    public bool IsFinished { get; private set => Set(ref field, value); }

    /// <summary>True when Home shows its one line: there are steps, and one is still open. Home then hides the setup rows of Needs you, which the line stands for.</summary>
    public bool IsHomeLineShown { get; private set => Set(ref field, value); }

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

    /// <summary>Asks the shell for Settings at the checklist (Home's line).</summary>
    internal void RequestChecklist() => ChecklistRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Asks the checklist to come into view.</summary>
    internal void RevealChecklist() => RevealRequested?.Invoke(this, new SetupRevealEventArgs(stepId: null));

    /// <summary>The first step still to do (or being worked on), as Home's line names it; empty until the first list.</summary>
    public string HomeStepText { get; private set; } = string.Empty;

    /// <summary>The Continue control's words.</summary>
    public string ContinueText { get; private set; } = string.Empty;

    /// <summary>Opens the first open step's row and asks for the checklist.</summary>
    internal void Continue() => throw new NotSupportedException(HomeStepText);

    /// <summary>Asks again, for every step, whether the app can do its action now.</summary>
    internal void Rederive()
    {
        foreach (var row in _rows.Values)
        {
            row.Rederive();
        }
    }

    /// <summary>Starts the map's refresh again (Retry on a failed map). One press sends once: the row shows as working until the next read, a refresh that is already running counts as running, and any other refusal is said in a sentence.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task RefreshMap(CancellationToken cancellationToken)
    {
        if (_refreshing || _url is not { } url || !_rows.TryGetValue(MapStep, out var map))
        {
            return;
        }

        _refreshing = true;
        try
        {
            var started = await _api.RefreshContextMap(url, cancellationToken).ConfigureAwait(true);
            if (started.IsSuccess)
            {
                map.ShowWorking(Labels.SetupMapAgain);
            }
            else if (string.Equals(started.Error.Code, RunningRefusal, StringComparison.Ordinal))
            {
                map.ShowWorking(Labels.SetupMapAlready);   // 409 with the running job: the same refresh is already going
            }
            else
            {
                map.Say(Labels.SetupMapRetryFailed);
            }
        }
        finally
        {
            _refreshing = false;
        }

        Fold();
    }

    // Everything that follows from the steps: finished or not, the count and the strip, and whether Home shows its line.
    private void Fold()
    {
        IsFinished = Steps.Count > 0 && Steps.All(s => s.IsFinished);
        HasSteps = Steps.Count > 0;
        CountText = HasSteps ? Labels.SetupCount(Steps.Count(s => s.IsFinished), Steps.Count) : string.Empty;
        OnChanged(nameof(HomeLineName));
        Lead = !HasSteps ? string.Empty : IsFinished ? Labels.SetupLeadDone : Labels.SetupLead;
        HasSub = HasSteps && !IsFinished;
        Segments = [.. Steps.Select(s => new SetupSegment(s.SegmentKind))];
        IsHomeLineShown = !IsReading && !IsNotServed && !IsUnreadable && HasSteps && !IsFinished;
    }

    // A step's action: an app button when the app can do it, else the command to copy; none for an action nothing here knows. A kept action stays while nothing about it changed, so a
    // Copy in flight is not reset.
    private SetupActionViewModel? ActionOf(SetupStepView step, SetupActionViewModel? kept)
    {
        if (Offered(step) is not { } action)
        {
            return null;
        }

        var inApp = _opensInApp(action) || OpensHere(action);
        if (kept is not null && kept.Matches(action, step, inApp))
        {
            return kept;
        }

        return Knows(action, inApp) ? new SetupActionViewModel(action, step, inApp, _catalog, _clock, Open) : null;
    }

    // The map has no button until it failed; then Retry is the catalog's refresh (a retry of the build, never a service run). Every other step offers what the API serves.
    private static string? Offered(SetupStepView step) =>
        string.Equals(step.Id, MapStep, StringComparison.Ordinal)
            ? string.Equals(step.State, "failed", StringComparison.Ordinal) ? MapRefresh : null
            : step.Action is { Length: > 0 } served ? served : null;

    // The refresh is the app's own button when the catalog lists it as ready.
    private bool OpensHere(string action) => string.Equals(action, MapRefresh, StringComparison.Ordinal) && _catalog.For(action) is { IsReady: true };

    private void Open(string action)
    {
        if (string.Equals(action, MapRefresh, StringComparison.Ordinal))
        {
            _ = RefreshMap(CancellationToken.None);
        }
        else
        {
            ActionRequested?.Invoke(this, new SetupActionEventArgs(action));
        }
    }

    private bool Knows(string action, bool inApp) => inApp || _catalog.Row(action) is not null || Labels.SetupCli(action).Length > 0;
}
