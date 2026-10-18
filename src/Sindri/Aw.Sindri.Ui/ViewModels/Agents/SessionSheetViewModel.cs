using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>
/// The session sheet (collector spec CU-3): one session's fields, host, state history and the pending Focus. Read-only; the Focus send is
/// U11a-f's.
/// </summary>
/// <param name="api">The API.</param>
internal sealed class SessionSheetViewModel(ISindriApi api) : ObservableObject
{
    /// <summary>The API the sheet reads.</summary>
    internal ISindriApi Api { get; } = api;

    /// <summary>The live preview's seam (CO-7c, CU-8): null until CO-7c types and fills it. The view's LivePreviewHost stays hidden and empty while it is null.</summary>
    public object? Preview { get; private set => Set(ref field, value); }

    /// <summary>The session's ref, provider/id; empty before the first.</summary>
    public string Ref { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the read stands.</summary>
    public FramePhase Phase { get; private set => Set(ref field, value); }

    /// <summary>True while a session's fields are shown.</summary>
    public bool IsShown => Phase == FramePhase.Shown;

    /// <summary>The provider and the title (or repo and id): "Claude Code private title".</summary>
    public string Heading { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The state line: chip, reason, tool and "estimated".</summary>
    public string StateLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The session's fields.</summary>
    public IReadOnlyList<SessionFieldViewModel> Fields { get; private set => Set(ref field, value); } = [];

    /// <summary>Where it runs.</summary>
    public IReadOnlyList<SessionFieldViewModel> HostFields { get; private set => Set(ref field, value); } = [];

    /// <summary>The states it was in, oldest first.</summary>
    public IReadOnlyList<StateHistoryEntryViewModel> History { get; private set => Set(ref field, value); } = [];

    /// <summary>True when there is a history to list.</summary>
    public bool HasHistory => History.Count > 0;

    /// <summary>True once the session has ended (the read answered 404).</summary>
    public bool IsEnded { get; private set => Set(ref field, value); }

    /// <summary>"This session has ended." while it has; empty otherwise.</summary>
    public string EndedText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The 404's served detail, for a tooltip only.</summary>
    public string EndedDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>A refusal's or an unreadable answer's sentence; empty otherwise.</summary>
    public string ProblemText { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The stale note: the last read failed and the fields are the last read's.</summary>
    public string Note { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The Focus control from the catalog; null while the catalog is unread or lists none.</summary>
    public ControlViewModel? Focus { get; private set => Set(ref field, value); }

    /// <summary>The pending Focus line: the phase and the CLI filled with the ref; empty without a Focus control.</summary>
    public string FocusLine { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The CLI to copy, filled with the ref.</summary>
    public string FocusCli { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Always false until U11a-f gives Focus a sender.</summary>
    public bool IsFocusEnabled { get; private set => Set(ref field, value); }

    /// <summary>"Copy", or "Copied" for a moment.</summary>
    public string CopyLabel { get; private set => Set(ref field, value); } = Labels.Copy;

    /// <summary>Gives the sheet the catalog (Focus's row) and the clock (Copy's "Copied").</summary>
    /// <param name="catalog">The command catalog.</param>
    /// <param name="clock">The clock.</param>
    internal void Wire(CatalogViewModel catalog, TimeProvider clock) => throw new NotSupportedException("T3");

    /// <summary>Names the session now, before any read (a deep link, even before the app is connected).</summary>
    /// <param name="reference">The session's ref, provider/id.</param>
    internal void Show(string reference) => throw new NotSupportedException("T3");

    /// <summary>Reads the session behind a ref and shows it.</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="reference">The session's ref, provider/id.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Open(Uri url, string reference, CancellationToken cancellationToken) => throw new NotSupportedException("T3");

    /// <summary>Reads the open session again (on every event while the sheet is open).</summary>
    /// <param name="url">The base URL.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>A task that ends when the read has been drawn.</returns>
    internal Task Refresh(Uri url, CancellationToken cancellationToken) => throw new NotSupportedException("T3");

    /// <summary>Copies the Focus CLI.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that ends when the clipboard answered.</returns>
    internal Task CopyFocus(CancellationToken cancellationToken) => throw new NotSupportedException("T3");
}
