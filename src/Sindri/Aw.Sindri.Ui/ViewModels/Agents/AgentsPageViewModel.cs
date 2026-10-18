using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Sessions;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The Agents page (collector spec CU-1, screens §4.5): the live agent sessions. T0's skeleton: the seams the shell and the sheet use
/// exist (<see cref="OpenRequested"/>, <see cref="Refresh"/>, <see cref="Press"/>); the list, filters, keys and polling are T1's, in the partials (.Refresh.cs, .Keys.cs).
/// </summary>
/// <param name="api">The API.</param>
/// <param name="clipboard">Where Copy puts text.</param>
/// <param name="clock">The clock the poll and the ages run on.</param>
internal sealed partial class AgentsPageViewModel(ISindriApi api, IClipboard clipboard, TimeProvider clock) : ObservableObject
{
    /// <summary>The API the page reads.</summary>
    internal ISindriApi Api { get; } = api;

    /// <summary>Where Copy puts text.</summary>
    internal IClipboard Clipboard { get; } = clipboard;

    /// <summary>The time on the page's clock (the poll and the ages run on it).</summary>
    internal DateTimeOffset Now => _clock.GetUtcNow();

    private readonly TimeProvider _clock = clock;

    /// <summary>Raised when a row asks to open its session's sheet; the shell's session-sheet partial handles it.</summary>
    internal event EventHandler<SessionOpenRequestedEventArgs>? OpenRequested;

    /// <summary>The text of the page's tab count; empty before the first status. The counts partial fills it.</summary>
    public string NavCount { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The chip for a served session row: its glyph and word (shared with the sheet).</summary>
    /// <param name="row">The row.</param>
    /// <returns>The chip.</returns>
    internal static SessionChip ChipOf(SessionRowView row) => SessionChips.For(row.State);

    /// <summary>Asks the shell to open a session's sheet.</summary>
    /// <param name="args">The link.</param>
    internal void RequestOpen(SessionOpenRequestedEventArgs args) => OpenRequested?.Invoke(this, args);
}
