using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// The command catalog as controls (UI spec §9.1: the only list of actions the app knows), read on connect and after a
/// reconnect. Each row becomes a <see cref="ControlViewModel"/>; only the actions the app has a sender for run. Health
/// lists the pending rows by phase (§4.8) and the palette lists every row (§8).
/// </summary>
/// <param name="clipboard">Where Copy puts a CLI.</param>
/// <param name="clock">The clock for the controls' timings.</param>
internal sealed class CatalogViewModel(IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    /// <summary>The catalog as last read; null before the first. Rows that carry the readiness of an action the API names.</summary>
    internal CommandList? Listed { get; private set; }

    /// <summary>Where Copy puts a CLI (a decision's line copies through it too).</summary>
    internal IClipboard Clipboard { get; } = clipboard;

    private Dictionary<string, CommandRowView> _rows = new(StringComparer.Ordinal);

    /// <summary>Every catalog row, in the API's order.</summary>
    public IReadOnlyList<ControlViewModel> Controls { get; private set => Set(ref field, value); } = [];

    /// <summary>The pending rows grouped by the phase that ships them, in the order the phases first appear (§4.8).</summary>
    public IReadOnlyList<PendingPhase> Pending { get; private set => Set(ref field, value); } = [];

    /// <summary>"N actions arrive in later phases", or empty when none are pending.</summary>
    public string PendingSummary { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The cli-only rows (copy blocks).</summary>
    public IReadOnlyList<ControlViewModel> CliOnly { get; private set => Set(ref field, value); } = [];

    /// <summary>The control for an action id, or null when the catalog has no such row.</summary>
    /// <param name="action">The action id.</param>
    /// <returns>The control.</returns>
    internal ControlViewModel? For(string action) => Controls.FirstOrDefault(c => string.Equals(c.Action, action, StringComparison.Ordinal));

    /// <summary>The catalog row for an action id, or null when the catalog is not read or has no such row.</summary>
    /// <param name="action">The action id.</param>
    /// <returns>The row.</returns>
    internal CommandRowView? Row(string action) => _rows.GetValueOrDefault(action);

    /// <summary>A control for one catalog row acting on one subject (an item's actions, a row's primary action), or null when the catalog has no such row.</summary>
    /// <param name="action">The action id.</param>
    /// <param name="subject">The subject's id, named in the control's accessible name.</param>
    /// <param name="send">Sends the command once; null when nothing is wired to it.</param>
    /// <returns>The control, or null.</returns>
    internal ControlViewModel? On(string action, string subject, Func<CancellationToken, Task<Result<CommandAcceptedView>>>? send) =>
        _rows.TryGetValue(action, out var row) ? new ControlViewModel(row, send, Clipboard, clock, subject) : null;

    /// <summary>Shows a catalog read from the API at <paramref name="api"/>.</summary>
    /// <param name="api">Where the API answers (the senders call it).</param>
    /// <param name="client">The API.</param>
    /// <param name="catalog">The catalog.</param>
    internal void Show(Uri api, ISindriApi client, CommandList catalog)
    {
        Listed = catalog;
        _rows = catalog.Rows.ToDictionary(r => r.Action, StringComparer.Ordinal);
        Controls = [.. catalog.Rows.Select(row => new ControlViewModel(row, Sender(api, client, row.Action), Clipboard, clock))];
        var pending = Controls.Where(c => c.IsPending).ToList();
        Pending = [.. pending.GroupBy(c => c.Stage, StringComparer.Ordinal).Select(g => new PendingPhase(Labels.Arrives(g.Key), [.. g]))];
        PendingSummary = pending.Count == 0 ? string.Empty : Labels.PendingSummary(pending.Count);
        CliOnly = [.. Controls.Where(c => c.IsCliOnly)];
    }

    // The actions U1 can send. Every other ready row waits for the phase that gives it a form or a confirm (StopProcess:
    // Health's confirm, U4).
    private static Func<CancellationToken, Task<Result<CommandAcceptedView>>>? Sender(Uri api, ISindriApi client, string action) => action switch
    {
        "RebuildViews" => ct => client.RebuildViews(api, ct),
        _ => null,
    };
}
