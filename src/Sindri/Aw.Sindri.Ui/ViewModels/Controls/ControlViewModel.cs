using System.Globalization;
using System.Text;
using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// One catalog row as a control (UI spec U3, §7) and its command lifecycle (§4.1). It maps the row's UI status to how
/// the control is drawn and says what the API answered; it decides nothing about whether the action is allowed: a
/// 'ready' row runs only when a sender is wired, and a 'pending' or 'cli-only' row never runs.
/// </summary>
internal sealed class ControlViewModel : ObservableObject
{
    /// <summary>How long a command is in flight before "Sending…" is announced.</summary>
    internal static readonly TimeSpan SendingAfter = TimeSpan.FromMilliseconds(300);

    /// <summary>How long the accepted outcome shows.</summary>
    internal static readonly TimeSpan AcceptedFor = TimeSpan.FromSeconds(2);

    /// <summary>How long "Copied" shows.</summary>
    internal static readonly TimeSpan CopiedFor = TimeSpan.FromMilliseconds(1500);

    private readonly Func<CancellationToken, Task<Result<CommandAcceptedView>>>? _send;
    private readonly IClipboard _clipboard;
    private readonly TimeProvider _clock;
    private int _runs;
    private int _copies;

    /// <summary>The control for <paramref name="row"/>.</summary>
    /// <param name="row">The catalog row.</param>
    /// <param name="send">Sends the command once; null when nothing is wired to it (it then never runs).</param>
    /// <param name="clipboard">Where Copy puts the CLI.</param>
    /// <param name="clock">The clock for the lifecycle's timings.</param>
    internal ControlViewModel(CommandRowView row, Func<CancellationToken, Task<Result<CommandAcceptedView>>>? send, IClipboard clipboard, TimeProvider clock)
    {
        _send = send;
        _clipboard = clipboard;
        _clock = clock;
        Action = row.Action;
        Label = Labels.Action(row.Action);
        Shortcut = row.Shortcut ?? string.Empty;
        (Kind, Stage) = Parse(row.Ui);
        Cli = Safe(row.Cli) ? row.Cli : string.Empty;
        PendingLine = Kind == ControlKind.Pending ? Labels.Arrives(Stage) + " · " + Cli : string.Empty;
        CopyNote = row.Ui == "cli-only:typed-confirmation" ? Labels.TypedConfirmation : string.Empty;
        Name = Kind == ControlKind.Pending ? Label + ", " + Labels.Arrives(Stage) : Label;
    }

    /// <summary>The catalog action id.</summary>
    public string Action { get; }

    /// <summary>The control's label (keyed by action id; an unknown id shows itself).</summary>
    public string Label { get; }

    /// <summary>The control's accessible name: its label, and its phase when pending.</summary>
    public string Name { get; }

    /// <summary>The catalog's default key, or empty.</summary>
    public string Shortcut { get; }

    /// <summary>How the control is drawn.</summary>
    public ControlKind Kind { get; }

    /// <summary>A pending row's phase ("P2"); empty otherwise.</summary>
    public string Stage { get; }

    /// <summary>True for a ready row.</summary>
    public bool IsReady => Kind == ControlKind.Ready;

    /// <summary>True for a pending row.</summary>
    public bool IsPending => Kind == ControlKind.Pending;

    /// <summary>True for a cli-only row.</summary>
    public bool IsCliOnly => Kind == ControlKind.CliOnly;

    /// <summary>The CLI as served, or empty when it holds a control or bidi character (§7's backstop).</summary>
    public string Cli { get; }

    /// <summary>A pending row's line: "arrives in P2 · sindri views rebuild".</summary>
    public string PendingLine { get; }

    /// <summary>A copy block's line ("Needs a typed confirmation at a terminal."), or empty.</summary>
    public string CopyNote { get; }

    /// <summary>True when the CLI can be copied.</summary>
    public bool CanCopy => Cli.Length > 0;

    /// <summary>The lifecycle phase.</summary>
    public CommandPhase Phase
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(CanRun));
            OnChanged(nameof(IsSending));
            OnChanged(nameof(IsAccepted));
            OnChanged(nameof(IsFailed));
        }
    }

    /// <summary>True when a click sends it: a ready row with a sender, not already in flight.</summary>
    public bool CanRun => IsReady && _send is not null && Phase != CommandPhase.Sending;

    /// <summary>True when the control is a live button: a ready row with a sender, even while it sends (so focus stays on it).</summary>
    public bool IsSendable => IsReady && _send is not null;

    /// <summary>True while sending (the spinner).</summary>
    public bool IsSending => Phase == CommandPhase.Sending;

    /// <summary>True while the accepted outcome shows.</summary>
    public bool IsAccepted => Phase == CommandPhase.Accepted;

    /// <summary>True after a refusal or failure.</summary>
    public bool IsFailed => Phase == CommandPhase.Failed;

    /// <summary>The accepted outcome in words, or empty.</summary>
    public string Outcome { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's SND code (ProblemDetails title), or empty.</summary>
    public string ErrorCode { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The refusal's detail as served, or empty.</summary>
    public string ErrorDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>A pending row's line with its subject: "Cancel job, arrives in P6 · sindri jobs cancel &lt;jobId&gt;".</summary>
    public string PendingName => PendingLine;

    /// <summary>The Copy button's accessible name: "Copy: Cancel job", then "Copied".</summary>
    public string CopyName => CopyLabel;

    /// <summary>"Copy", or "Copied" for 1.5 s after a copy.</summary>
    public string CopyLabel { get; private set => Set(ref field, value); } = Labels.Copy;

    /// <summary>True while "Sending…" is announced (the live region's gate, set before its text changes).</summary>
    public bool IsAnnouncingSending { get; private set => Set(ref field, value); }

    /// <summary>The polite live region's text: "Sending…", the outcome, "Copied"; never empty (Avalonia #22312).</summary>
    public string Announcement { get; private set => Set(ref field, value); } = Labels.Copy;

    /// <summary>Sends the command once (a second click while sending does nothing; the app never retries on its own).</summary>
    /// <param name="cancellationToken">Cancels the wait for the answer.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Run(CancellationToken cancellationToken)
    {
        if (!CanRun || _send is not { } send)
        {
            return;
        }

        var run = ++_runs;
        (ErrorCode, ErrorDetail, Outcome) = (string.Empty, string.Empty, string.Empty);
        Announcement = Label;   // a resting text first, so an outcome the same as the last one is still a change to the live region
        Phase = CommandPhase.Sending;
        _ = AnnounceSending(run, cancellationToken);
        var answer = await send(cancellationToken).ConfigureAwait(true);
        IsAnnouncingSending = false;
        if (answer.IsError)
        {
            (ErrorCode, ErrorDetail) = (answer.Error.Code, answer.Error.Message);
            Announcement = $"{ErrorCode}: {ErrorDetail}";
            Phase = CommandPhase.Failed;
            return;
        }

        Outcome = Labels.Done(Label);
        Announcement = Outcome;
        Phase = CommandPhase.Accepted;
        _ = Settle(run, cancellationToken);
    }

    /// <summary>Copies the CLI (never one with a control or bidi character): "Copied" for 1.5 s, announced.</summary>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task that completes once the clipboard answered.</returns>
    internal async Task Copy(CancellationToken cancellationToken)
    {
        if (!CanCopy || !await _clipboard.SetText(Cli, cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        (CopyLabel, Announcement) = (Labels.Copied, Labels.Copied);
        _ = Uncopy(++_copies, cancellationToken);
    }

    private async Task Uncopy(int copy, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(CopiedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (copy == _copies)
        {
            CopyLabel = Labels.Copy;
        }
    }

    private async Task AnnounceSending(int run, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SendingAfter, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (run == _runs && Phase == CommandPhase.Sending)
        {
            IsAnnouncingSending = true;
            Announcement = Labels.Sending;
        }
    }

    private async Task Settle(int run, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(AcceptedFor, _clock, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (run == _runs && Phase == CommandPhase.Accepted)
        {
            Phase = CommandPhase.Idle;
        }
    }

    private static (ControlKind Kind, string Stage) Parse(string ui) => ui switch
    {
        "ready" => (ControlKind.Ready, string.Empty),
        _ when ui.StartsWith("pending:", StringComparison.Ordinal) => (ControlKind.Pending, ui["pending:".Length..]),
        _ => (ControlKind.CliOnly, string.Empty),
    };

    // §7: the app refuses to show or copy a cli with a control, bidi or invisible character (E-16 makes this never happen).
    private static bool Safe(string cli) => cli.Length > 0 && !cli.EnumerateRunes().Any(r => r.Value != ' ' && Rune.GetUnicodeCategory(r) is
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
        or UnicodeCategory.SpaceSeparator or UnicodeCategory.PrivateUse or UnicodeCategory.Surrogate or UnicodeCategory.OtherNotAssigned);
}
