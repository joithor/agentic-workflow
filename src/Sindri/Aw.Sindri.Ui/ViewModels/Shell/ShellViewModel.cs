using System.ComponentModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Item;
using Aw.Sindri.Ui.ViewModels.Palette;
using Aw.Sindri.Ui.ViewModels.Queue;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The window (UI spec §3.2, §4.1): the top bar's navigation and health pill, the current page, the pre-API state, and
/// the detail sheet. It maps what the API and the platform say to words and visibility, and holds no other logic: the
/// connection and its state live in <see cref="Health"/>, and the window's size, the sheet's width and the last page are
/// local preferences, read once by <see cref="Restore"/> and written over the stored file when they change.
/// </summary>
internal sealed partial class ShellViewModel : ObservableObject
{
    /// <summary>How long without an event or a ping before the views are stale (§4.1, API §2.4).</summary>
    internal static readonly TimeSpan StaleAfter = HealthViewModel.StaleAfter;

    private static readonly string[] _stateProperties =
    [
        nameof(State), nameof(IsVersionMismatch), nameof(IsChromeVisible), nameof(IsNotRunning), nameof(IsNotRunningPanel), nameof(IsConnecting),
        nameof(IsUnreadable), nameof(IsNotRespondingBanner), nameof(IsPageVisible), nameof(IsSheetShown), nameof(IsEdgeTabShown), nameof(IsNavigationEnabled),
    ];

    private static readonly string[] _pageProperties =
    [
        nameof(CurrentPage), nameof(PageName), nameof(SelectedNav), nameof(IsHome), nameof(IsQueue), nameof(IsDecisions), nameof(IsServices),
        nameof(IsHealth), nameof(IsSettings), nameof(IsNotRunningPanel), nameof(IsPageVisible),
    ];

    private readonly Preferences _store;
    private UiPreferences _preferences = UiPreferences.Defaults;

    // The page Settings was opened from, where leaving it over the Not running card returns.
    private PageId _beforeSettings = PageId.Home;

    /// <summary>The shell over its ports.</summary>
    /// <param name="records">Where the control plane recorded its endpoint.</param>
    /// <param name="api">Its API.</param>
    /// <param name="clock">The clock for every timing rule.</param>
    /// <param name="preferences">The app's local preferences, shared with Settings (the shell keeps its window, sheet width and last page there).</param>
    /// <param name="clipboard">Where Copy puts a CLI (the window's clipboard); none by default.</param>
    internal ShellViewModel(IEndpointRecordSource records, ISindriApi api, TimeProvider clock, Preferences preferences, IClipboard? clipboard = null)
    {
        _store = preferences;
        _clock = clock;
        Health = new HealthViewModel(records, api, clock, clipboard);
        Health.PropertyChanged += OnHealthChanged;
        Health.Pill.PropertyChanged += OnPillChanged;
        Palette = new CommandPaletteViewModel(Health.Catalog, GoTo, RememberRun);
        Shortcuts = new ShortcutsViewModel(Health.Catalog);
        Queue = new QueueViewModel(api, Health.Catalog, clock);
        Item = new ItemSheetViewModel(api, Health.Catalog, clock);
        Create = new CreateItemViewModel(Health.Catalog, () => Health.ApiUrl(), api);
        Health.Follow(Queue.Refresh);
        Health.Follow(Item.Refresh);
        Queue.PropertyChanged += OnQueueChanged;
    }

    /// <summary>The app's copy, for the view.</summary>
    public ShellText Text { get; } = ShellText.Instance;

    /// <summary>The connection and the Health page's status card.</summary>
    public HealthViewModel Health { get; }

    /// <summary>The top bar's segments, in order: Home, Queue, Decisions, Services.</summary>
    public IReadOnlyList<NavEntry> Navigation { get; private set; } = Nav(queued: string.Empty);

    /// <summary>"Later ▾": Agents, Repos, Today, Automatic work, each "not served yet".</summary>
    public IReadOnlyList<LaterEntry> Later { get; } =
    [
        new(Labels.Agents, Labels.NotServedYet),
        new(Labels.Repos, Labels.NotServedYet),
        new(Labels.Today, Labels.NotServedYet),
        new(Labels.AutomaticWork, Labels.NotServedYet),
    ];

    /// <summary>Every page the window can show, in order: Home, Queue, Decisions, Services, Health, Settings.</summary>
    public IReadOnlyList<PageId> Pages { get; } = Enum.GetValues<PageId>();

    /// <summary>The page shown.</summary>
    public PageId CurrentPage { get; private set; } = PageId.Home;

    /// <summary>The top bar's segment for the page shown; null on Health and Settings. Setting it goes to its page.</summary>
    public NavEntry? SelectedNav
    {
        get => Navigation.FirstOrDefault(n => n.Page == CurrentPage);
        set
        {
            if (value is not null)
            {
                GoTo(value.Page);
            }
        }
    }

    /// <summary>The page region's accessible name: the page shown, in words.</summary>
    public string PageName => Labels.Page(CurrentPage);

    /// <summary>True on Home.</summary>
    public bool IsHome => CurrentPage == PageId.Home;

    /// <summary>True on Queue.</summary>
    public bool IsQueue => CurrentPage == PageId.Queue;

    /// <summary>True on Decisions.</summary>
    public bool IsDecisions => CurrentPage == PageId.Decisions;

    /// <summary>True on Services.</summary>
    public bool IsServices => CurrentPage == PageId.Services;

    /// <summary>True on Health.</summary>
    public bool IsHealth => CurrentPage == PageId.Health;

    /// <summary>True on Settings.</summary>
    public bool IsSettings => CurrentPage == PageId.Settings;

    /// <summary>The window's pre-API state.</summary>
    public ShellState State => Health.State;

    /// <summary>The state's reason in words: why it isn't running, the two versions, the unreadable answer's code and message; empty when Normal.</summary>
    public string Reason => Health.Reason;

    /// <summary>The not-running reason's detail line (a path, an address, a pid and epoch), or empty.</summary>
    public string ReasonDetail => Health.ReasonDetail;

    /// <summary>The health pill's parts: its dot, glyph, word and age, and Refresh while stale.</summary>
    public HealthPill Pill => Health.Pill;

    /// <summary>Version mismatch: the full-window panel, and nothing else.</summary>
    public bool IsVersionMismatch => State == ShellState.VersionMismatch;

    /// <summary>The top bar shows in every state but a version mismatch.</summary>
    public bool IsChromeVisible => !IsVersionMismatch;

    /// <summary>The top bar's navigation takes input (dimmed and disabled while not running).</summary>
    public bool IsNavigationEnabled => !IsNotRunning;

    /// <summary>Not running: S1b's panel in place of the page.</summary>
    public bool IsNotRunning => State == ShellState.NotRunning;

    /// <summary>Not running: S1b's card, unless Settings (local preferences, reachable in any state) shows over it.</summary>
    public bool IsNotRunningPanel => IsNotRunning && !IsSettings;

    /// <summary>Connecting: skeleton rows in place of the page (first load only).</summary>
    public bool IsConnecting => State == ShellState.Connecting;

    /// <summary>Unreadable: the banner above the page, which keeps its last data.</summary>
    public bool IsUnreadable => State == ShellState.Unreadable;

    /// <summary>Stale after three failed refreshes: the banner under the top bar that says "Sindri not responding: run sindri doctor" (§4.1).</summary>
    public bool IsNotRespondingBanner => State == ShellState.Stale && Pill.IsNotResponding;

    /// <summary>The page shows when connected (Normal, Stale, or Unreadable with the last data), and Settings over the Not running card.</summary>
    public bool IsPageVisible => IsConnected || (IsSettings && IsNotRunning);

    // Connected: the API's pages (and the sheet over them) have something to show.
    private bool IsConnected => State is ShellState.Normal or ShellState.Stale or ShellState.Unreadable;

    /// <summary>True while the detail sheet is open.</summary>
    public bool IsSheetOpen { get; private set; }

    /// <summary>The sheet's subject (the deep link's subject); kept after the sheet closes for the edge tab; empty before the first.</summary>
    public string SheetSubject { get; private set; } = string.Empty;

    /// <summary>True when the closed sheet shows its edge tab (a subject was opened before).</summary>
    public bool HasEdgeTab => SheetSubject.Length > 0;

    /// <summary>The sheet shows when it's open over a visible page.</summary>
    public bool IsSheetShown => IsSheetOpen && IsConnected;

    /// <summary>The edge tab's accessible name: "Reopen" and its subject.</summary>
    public string EdgeTabName => Labels.Reopen + " " + SheetSubject;

    /// <summary>The edge tab shows when the sheet is closed and has a subject, over a visible page.</summary>
    public bool IsEdgeTabShown => !IsSheetOpen && HasEdgeTab && IsConnected;

    /// <summary>The attention band under the top bar (§3.2): always false in U0, since needs-you arrives in U3.</summary>
    public bool IsAttentionBandVisible { get; }

    /// <summary>The window's remembered bounds, or null to let the platform place it.</summary>
    public WindowBounds? Window => _preferences.Window;

    /// <summary>The detail sheet's width (420–760).</summary>
    public double SheetWidth => _preferences.SheetWidth;

    /// <summary>Shows a page (the G-chords, the top bar, the pill) and remembers it.</summary>
    /// <param name="page">The page.</param>
    internal void GoTo(PageId page)
    {
        if (page == CurrentPage)
        {
            return;
        }

        if (page == PageId.Settings)
        {
            _beforeSettings = CurrentPage;
        }

        CurrentPage = page;
        Changed(_pageProperties);
        Remember(p => p with { LastPage = page.ToString() });
    }

    /// <summary>
    /// Esc on Settings over the Not running card: back to the page it was opened from, so the card shows again. While
    /// connected Settings is a page like any other, and Esc leaves it where it is.
    /// </summary>
    /// <returns>True when it left Settings.</returns>
    internal bool LeaveSettings()
    {
        if (!IsSettings || !IsNotRunning)
        {
            return false;
        }

        GoTo(_beforeSettings);
        return true;
    }

    /// <summary>Follows the control plane until cancelled (S1b's loop: control.json, ping, status, events).</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal Task Run(CancellationToken cancellationToken) => Health.Run(cancellationToken);

    private void OnHealthChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(HealthViewModel.State):
                Changed(_stateProperties);
                Announce();
                break;
            case nameof(HealthViewModel.Reason):
                OnChanged(e.PropertyName);
                Announce();
                break;
            case nameof(HealthViewModel.ReasonDetail):
                OnChanged(e.PropertyName);
                break;
            default:
                break;
        }
    }

    private void OnPillChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HealthPill.IsNotResponding))
        {
            OnChanged(nameof(IsNotRespondingBanner));
            Announce();
        }
    }

    private void Changed(params string[] names)
    {
        foreach (var name in names)
        {
            OnChanged(name);
        }
    }
}
