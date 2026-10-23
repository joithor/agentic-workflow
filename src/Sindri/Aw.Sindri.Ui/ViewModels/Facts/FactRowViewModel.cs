using System.Globalization;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Facts;

/// <summary>
/// One row of the facts panel: the mark, the label, the summary as served, who and when, the progress, the state it came from, and View,
/// which opens the fact's viewer under the row. A pending output a desk step completes shows what it awaits, the CLI that completes it
/// with a Copy, and a chip that says the form arrives with U8b.
/// </summary>
internal sealed class FactRowViewModel : ObservableObject
{
    private const string HumanPrefix = "human:";

    private readonly FactRow _row;
    private readonly FactViewerRegistry _registry;
    private readonly FactViewerContext _context;

    internal FactRowViewModel(FactRow row, string address, IReadOnlyList<StateCell> states, StatusCommand? complete, FactViewerRegistry registry, FactViewerContext context, Func<string, string>? nameOf = null)
    {
        (_row, _registry, _context) = (row, registry, context);
        (Key, Label, Address) = (row.Key, FactText.Scrub(row.Label), address);
        Glyph = Labels.FactMark(row.Status);
        StatusWords = Labels.FactStatusWords(row.Status);
        Summary = FactText.Scrub(row.Summary);
        By = row.By is null ? string.Empty : row.By.StartsWith(HumanPrefix, StringComparison.Ordinal) ? Labels.ByYou : FactText.Scrub(row.By);
        At = row.At is { } at ? Local(at, context.Clock) : string.Empty;
        ProgressText = row.Progress is { } progress ? Labels.ProgressOf(progress) : string.Empty;
        (From, FromMark) = Origin(row.From, states, nameOf);
        CanView = row.Status != FactStatus.Pending;
        IsAwaiting = row.Status == FactStatus.Pending && row.Pending is not null;
        PendingChip = IsAwaiting ? Labels.AvailableWithU8b : string.Empty;
        PendingCli = IsAwaiting ? CompletingCli(row, complete) : string.Empty;
        CopyingCli = new CopyAction(context.Clipboard, context.Clock, row.Label);
        CopyCliName = $"{Labels.Copy}: {(PendingCli.Length > 0 ? PendingCli : Labels.NoCommandServed)}";
    }

    /// <summary>The fact's key.</summary>
    public string Key { get; }

    /// <summary>The label as served.</summary>
    public string Label { get; }

    /// <summary>The instance's address.</summary>
    public string Address { get; }

    /// <summary>●, ◐ or ○.</summary>
    public string Glyph { get; }

    /// <summary>The status in words.</summary>
    public string StatusWords { get; }

    /// <summary>The summary as served.</summary>
    public string Summary { get; }

    /// <summary>Who produced it: "you" for a human, else as served; empty when none.</summary>
    public string By { get; }

    /// <summary>When, in the local time; empty when none.</summary>
    public string At { get; }

    /// <summary>"4 of 9"; empty when none.</summary>
    public string ProgressText { get; }

    /// <summary>True when the row serves a progress figure.</summary>
    public bool HasProgress => ProgressText.Length > 0;

    /// <summary>The progress done, for a bar.</summary>
    public double ProgressDone => _row.Progress?.Done ?? 0;

    /// <summary>The progress total, for a bar.</summary>
    public double ProgressTotal => _row.Progress?.Total ?? 0;

    /// <summary>The state it came from ("from scoping"); empty for an input.</summary>
    public string From { get; }

    /// <summary>The band's mark for the state it came from; empty for an input.</summary>
    public string FromMark { get; }

    /// <summary>The words that follow the label: who, when and where from.</summary>
    public string Meta => string.Join(" · ", new[] { By, At, From.Length > 0 ? $"{From} {FromMark}" : string.Empty }.Where(p => p.Length > 0));

    /// <summary>True for a pending output the desk completes.</summary>
    public bool IsAwaiting { get; }

    /// <summary>The CLI that completes it, or empty.</summary>
    public string PendingCli { get; }

    /// <summary>True when the row shows a CLI.</summary>
    public bool HasPendingCli => PendingCli.Length > 0;

    /// <summary>The chip that says the form arrives later.</summary>
    public string PendingChip { get; }

    /// <summary>Copy of the pending CLI.</summary>
    public CopyAction CopyingCli { get; }

    /// <summary>The Copy button's accessible name: it carries the CLI.</summary>
    public string CopyCliName { get; }

    /// <summary>True when the row can be viewed (anything produced or in progress).</summary>
    public bool CanView { get; }

    /// <summary>True while the viewer is open.</summary>
    public bool IsViewing { get; private set { Set(ref field, value); OnChanged(nameof(ViewLabel)); OnChanged(nameof(ViewName)); } }

    /// <summary>The open viewer; null while closed.</summary>
    public IFactViewer? Viewer { get; private set => Set(ref field, value); }

    /// <summary>"View" or "Hide".</summary>
    public string ViewLabel => IsViewing ? Labels.HideFact : Labels.ViewFact;

    /// <summary>The View button's accessible name: the action, the fact's label and the instance.</summary>
    public string ViewName => Labels.ViewFactName(IsViewing, Label, Address);

    /// <summary>The row's accessible name: label, key, status in words and summary.</summary>
    public string Name => $"{Label}, {StatusWords}, {Summary}";

    /// <summary>What the row draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('|', Key, Label, Glyph, Summary, Meta, ProgressText, PendingCli);

    /// <summary>Opens or closes the viewer.</summary>
    public void ToggleView()
    {
        if (!CanView)
        {
            return;
        }

        if (IsViewing)
        {
            (IsViewing, Viewer) = (false, null);
            return;
        }

        var viewer = _registry.Create(_row, _context);
        (Viewer, IsViewing) = (viewer, true);
        _ = viewer.Load(CancellationToken.None);
    }

    /// <summary>Copies the pending CLI.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    public Task CopyCli(CancellationToken cancellationToken) => CopyingCli.Run(PendingCli, cancellationToken);

    // The desk's complete command names the outputs it takes; this row's is the one that names its key.
    private static string CompletingCli(FactRow row, StatusCommand? complete) =>
        complete is { } c && c.Cli.Contains(row.Key, StringComparison.Ordinal) && ControlViewModel.Safe(c.Cli) ? c.Cli : string.Empty;

    // "from <state>" in the recipe's words, and the band's mark for that state; both empty for an input.
    private static (string From, string Mark) Origin(string? from, IReadOnlyList<StateCell> states, Func<string, string>? nameOf) =>
        from is null ? (string.Empty, string.Empty) : (Labels.FromState(nameOf?.Invoke(from) ?? from), Mark(states, from));

    private static string Mark(IReadOnlyList<StateCell> states, string state) =>
        states.FirstOrDefault(s => string.Equals(s.State, state, StringComparison.Ordinal)) is { } cell ? new StageCellViewModel(cell, isCurrent: false, isBlocked: false).Glyph : string.Empty;

    // Today's facts read as a time; an older one adds its day.
    private static string Local(DateTimeOffset at, TimeProvider clock)
    {
        var (then, now) = (TimeZoneInfo.ConvertTime(at, clock.LocalTimeZone), TimeZoneInfo.ConvertTime(clock.GetUtcNow(), clock.LocalTimeZone));
        return then.Date == now.Date ? then.ToString("HH:mm", CultureInfo.InvariantCulture) : then.ToString("MM-dd HH:mm", CultureInfo.InvariantCulture);
    }
}
