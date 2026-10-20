using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Components;

namespace Aw.Sindri.Ui.ViewModels.Controls;

/// <summary>
/// The typed confirmation's state (setup spec §2.1): the person types the server's own text, and only exactly that, after trimming and with
/// case, presses the button. It draws and decides the match; the host owns the challenge and sends the answer. One press sends once: while
/// the answer is out the field is empty and the button is off.
/// </summary>
internal sealed class TypedConfirmBoxViewModel : ObservableObject
{
    private string Action { get; }

    /// <summary>A typed confirmation for one of the three approvals the app confirms.</summary>
    /// <param name="action">ApproveProfile, ApproveRecipe or ApproveAllRecipes; any other id is refused.</param>
    /// <param name="catalog">The command catalog: its row says whether the app may confirm or the terminal copy shows.</param>
    /// <param name="clock">The clock the terminal copy's "Copied" runs on.</param>
    internal TypedConfirmBoxViewModel(string action, CatalogViewModel catalog, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(clock);
        Action = AppConfirm.Require(action);
        Terminal = AppConfirm.Terminal(catalog, action, Labels.TerminalVerb(action), Labels.ConfirmSubject(action), clock);
        Live = new LiveAnnouncer(Labels.TypedButton(action));
    }

    /// <summary>The polite live region: the mismatch, said when the person presses on a wrong phrase, never per key.</summary>
    public LiveAnnouncer Live { get; }

    /// <summary>True when the catalog says the app may confirm; otherwise the terminal copy shows in its place.</summary>
    public bool IsReady => Terminal is null;

    /// <summary>The terminal copy for a row that is not ready; null when it is.</summary>
    public ProtectedActionViewModel? Terminal { get; }

#pragma warning disable CA1822, MA0041 // fixed words are properties because the view binds them
    /// <summary>The lead-in before the phrase.</summary>
    public string Ask => Labels.TypedAsk;

    /// <summary>The note under the box.</summary>
    public string Note => Labels.TypedNote;

    /// <summary>The field's accessible name.</summary>
    public string FieldName => Labels.ApproveTypeName;
#pragma warning restore CA1822, MA0041

    /// <summary>The button's words and accessible name: it says what it approves.</summary>
    public string ButtonLabel => Labels.TypedButton(Action);

    /// <summary>The text to type, as the server served it; empty before a challenge.</summary>
    public string Phrase { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>What the person has typed.</summary>
    public string Text
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(Matches));
            OnChanged(nameof(CanSubmit));
            OnChanged(nameof(ShowsMismatch));
        }
    } = string.Empty;

    /// <summary>True while the answer is out.</summary>
    public bool IsSending { get; private set => Set(ref field, value); }

    /// <summary>True when the typed text, trimmed, is exactly the phrase (case counts).</summary>
    public bool Matches => Phrase.Length > 0 && string.Equals(Text.Trim(), Phrase, StringComparison.Ordinal);

    /// <summary>True when the button can be pressed.</summary>
    public bool CanSubmit => Matches && !IsSending;

    /// <summary>True when something is typed that is not the phrase.</summary>
    public bool ShowsMismatch => Text.Trim().Length > 0 && !Matches;

    /// <summary>Takes the served challenge: its text, else the alternative it carries, is the phrase.</summary>
    /// <param name="challenge">The challenge.</param>
    internal void Offer(AppChallengeView challenge)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        Phrase = challenge.TypedAlternative ?? challenge.Action;
        OnChanged(nameof(Matches));
        OnChanged(nameof(CanSubmit));
        OnChanged(nameof(ShowsMismatch));
    }

    /// <summary>The press (the button, or Enter in the field).</summary>
    /// <returns>The typed text when it matches and nothing is out, and the host should send it; otherwise null, with the mismatch said once when there is one.</returns>
    internal string? Submit()
    {
        if (CanSubmit)
        {
            return Text.Trim();
        }

        if (ShowsMismatch && !IsSending)
        {
            Live.Say(Labels.ApproveTypedMismatch(Phrase));
        }

        return null;
    }

    /// <summary>The answer is out (the field empties and goes off) or it came back (the field is there again).</summary>
    /// <param name="sending">True while the answer is out.</param>
    internal void SetSending(bool sending)
    {
        IsSending = sending;
        if (sending)
        {
            Text = string.Empty;
        }
        else
        {
            OnChanged(nameof(CanSubmit));
        }
    }

    /// <summary>No challenge: the phrase and the field are empty.</summary>
    internal void Clear()
    {
        Phrase = string.Empty;
        Text = string.Empty;
    }
}
