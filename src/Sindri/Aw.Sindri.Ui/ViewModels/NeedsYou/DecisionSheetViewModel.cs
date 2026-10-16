using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The decision sheet (U3' T6): the question, one button per option as served, the note, and what Decide answered. A decide is
/// sent once and never retried; a refusal (decided elsewhere, voided, not found) shows as served, inline. When the row clears
/// while the sheet is open the form is disabled and keeps its text.
/// </summary>
internal sealed class DecisionSheetViewModel(ISindriApi api, Func<Uri?> apiUrl) : ObservableObject
{
    private readonly HashSet<string> _sent = new(StringComparer.Ordinal);
    private NeedsYouRowViewModel? _row;

    /// <summary>Raised when Decide was refused, so the shell can open the sheet where the refusal shows.</summary>
    internal event EventHandler? Refused;

    /// <summary>The option buttons, as served (the first is the recommended one).</summary>
    internal IReadOnlyList<DecisionOption> Options => _row is null ? [] :
        [.. _row.Controls.Where(c => c.Action.OptionId.Length > 0).Select((c, i) => new DecisionOption(c.Action.OptionId, c.Action.Label, i == 0, c.IsEnabled && CanSend))];

    /// <summary>The decision's question (the row's title).</summary>
    internal string Question => _row?.Title ?? "";

    /// <summary>The recorded-only sentence as served; empty for none.</summary>
    internal string Notice => _row?.Notice ?? "";

    /// <summary>The id of the decision shown.</summary>
    internal string DecisionId { get; private set; } = "";

    /// <summary>The note sent with the choice.</summary>
    internal string Note
    {
        get;
        set => Set(ref field, value);
    } = "";

    /// <summary>The served refusal's detail; empty for none.</summary>
    internal string Refusal { get; private set => Set(ref field, value); } = "";

    /// <summary>The served refusal's code; empty for none.</summary>
    internal string RefusalCode { get; private set => Set(ref field, value); } = "";

    /// <summary>What was decided, once Decide was accepted; empty before.</summary>
    internal string Outcome { get; private set => Set(ref field, value); } = "";

    /// <summary>True while a Decide is in flight.</summary>
    internal bool IsSending { get; private set => Set(ref field, value); }

    /// <summary>True when the row left the list while the sheet was open.</summary>
    internal bool IsCleared { get; private set => Set(ref field, value); }

    /// <summary>The cleared line, once the row is gone.</summary>
    internal string ClearedLine => IsCleared ? Labels.Cleared(DecisionId) : "";

    /// <summary>The polite live region: set true before the text, spoken once.</summary>
    internal bool IsAnnouncing { get; private set => Set(ref field, value); }

    /// <summary>The live region's text.</summary>
    internal string Announcement { get; private set => Set(ref field, value); } = Labels.Sindri;

    /// <summary>The last send; completes when Decide was answered.</summary>
    internal Task Settled { get; private set; } = Task.CompletedTask;

    private bool CanSend => !IsSending && !IsCleared && Outcome.Length == 0;

    /// <summary>Shows a row's decision; another decision starts a fresh form, the same one is updated in place.</summary>
    /// <param name="row">The decision row.</param>
    internal void Show(NeedsYouRowViewModel row)
    {
        var id = row.Entry.Subject?.Id ?? row.Id;
        if (!string.Equals(id, DecisionId, StringComparison.Ordinal) || IsCleared)
        {
            (Note, Refusal, RefusalCode, Outcome, IsCleared) = ("", "", "", "", false);
            DecisionId = id;
            OnChanged(nameof(ClearedLine));
        }

        _row = row;
        Raise();
    }

    /// <summary>The row left the list: the form is disabled and keeps its text.</summary>
    internal void Clear()
    {
        if (_row is null || IsCleared)
        {
            return;
        }

        IsCleared = true;
        OnChanged(nameof(ClearedLine));
        Raise();
    }

    /// <summary>True when this row is the decision shown.</summary>
    /// <param name="rowId">The row's id.</param>
    /// <returns>True when it is.</returns>
    internal bool Shows(string rowId) => _row is { } row && string.Equals(row.Id, rowId, StringComparison.Ordinal);

    /// <summary>Sends Decide with an option and the note; once per decision, never retried. Does nothing while sending or after the decision was accepted.</summary>
    /// <param name="optionId">The option.</param>
    /// <returns>A task that completes when Decide was answered.</returns>
    internal Task Choose(string optionId)
    {
        if (!CanSend || _row is null || _sent.Contains(DecisionId) || apiUrl() is not { } url)
        {
            return Task.CompletedTask;
        }

        IsSending = true;
        Raise();
        Settled = Send(url, optionId);
        return Settled;
    }

    private async Task Send(Uri url, string optionId)
    {
        var label = Options.FirstOrDefault(o => string.Equals(o.Id, optionId, StringComparison.Ordinal))?.Label ?? optionId;
        var answer = await api.Decide(url, DecisionId, new DecideRequest(optionId, Note.Length == 0 ? null : Note), CancellationToken.None).ConfigureAwait(true);
        IsSending = false;
        if (answer.IsError)
        {
            (RefusalCode, Refusal) = (answer.Error.Code, answer.Error.Message);
            Say(Refusal);
            Raise();
            Refused?.Invoke(this, EventArgs.Empty);
            return;
        }

        _ = _sent.Add(DecisionId);
        Outcome = Labels.Decided(label);
        Say(Outcome);
        Raise();
    }

    private void Raise()
    {
        OnChanged(nameof(Options));
        OnChanged(nameof(Question));
        OnChanged(nameof(Notice));
    }

    // The flag changes before the text, and an announcement already live is dropped first so the next one is spoken again.
    private void Say(string text)
    {
        if (IsAnnouncing)
        {
            IsAnnouncing = false;
        }

        Announcement = Labels.Sindri;
        IsAnnouncing = true;
        Announcement = text;
    }
}
