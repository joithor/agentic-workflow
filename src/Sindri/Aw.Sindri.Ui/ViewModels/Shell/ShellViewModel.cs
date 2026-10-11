using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The window (UI spec §3.2, §4.1): the top bar's navigation and health pill, the current page, the pre-API state, and
/// the detail sheet. It maps what the API and the platform say to words and visibility, and holds no other logic.
/// CONTRACT (U0 Unit C): the surface Unit A implements; the constructor gains its ports (the endpoint record, the API,
/// the clock, the preferences) in Unit A's implement step. Until then every member is a stub.
/// </summary>
[SuppressMessage("Design", "MA0182:Internal type is apparently never used", Justification = "U0 contract: Unit A builds it in SindriApp.Create(SindriPlatform, TimeProvider); until then only its red tests use it.")]
internal sealed class ShellViewModel : ObservableObject
{
    /// <summary>How long without an event or a ping before the views are stale (§4.1, API §2.4).</summary>
    internal static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(30);

    /// <summary>The top bar's segments, in order: Home, Queue, Decisions, Services.</summary>
    public IReadOnlyList<NavEntry> Navigation { get; } = [];

    /// <summary>"Later ▾": Agents, Repos, Today, Automatic work, each "not served yet".</summary>
    public IReadOnlyList<LaterEntry> Later { get; } = [];

    /// <summary>Every page the window can show, in order: Home, Queue, Decisions, Services, Health, Settings.</summary>
    public IReadOnlyList<PageId> Pages { get; } = [];

    /// <summary>The page shown.</summary>
    public PageId CurrentPage { get; }

    /// <summary>The window's pre-API state.</summary>
    public ShellState State { get; }

    /// <summary>The state's reason in words: why it isn't running, the two versions, the unreadable answer's code and message; empty when Normal.</summary>
    public string Reason { get; } = string.Empty;

    /// <summary>The health pill: the state word, and "updated Ns ago" (or "stale") once connected. It opens Health.</summary>
    public string HealthPill { get; } = string.Empty;

    /// <summary>True while the detail sheet is open.</summary>
    public bool IsSheetOpen { get; }

    /// <summary>The sheet's subject (the deep link's subject); kept after the sheet closes for the edge tab; empty before the first.</summary>
    public string SheetSubject { get; } = string.Empty;

    /// <summary>True when the closed sheet shows its edge tab (a subject was opened before).</summary>
    public bool HasEdgeTab { get; }

    /// <summary>The attention band under the top bar (§3.2): always false in U0, since needs-you arrives in U3.</summary>
    public bool IsAttentionBandVisible { get; }

    /// <summary>Shows a page (the G-chords, the top bar, the pill).</summary>
    /// <param name="page">The page.</param>
    internal void GoTo(PageId page) => throw new NotSupportedException();

    /// <summary>Goes where a deep link points: a page, or the detail sheet with its subject.</summary>
    /// <param name="link">The link.</param>
    internal void Navigate(DeepLink link) => throw new NotSupportedException();

    /// <summary>Closes the sheet (Esc); its subject stays on the edge tab.</summary>
    internal void CloseSheet() => throw new NotSupportedException();

    /// <summary>Follows the control plane until cancelled (S1b's loop: control.json, ping, status, events).</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal Task Run(CancellationToken cancellationToken) => throw new NotSupportedException();
}
