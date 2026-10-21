using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.ViewModels.Setup;

#pragma warning disable CA1822, MA0041, IDE0051 // the red commit's stubs: no behaviour yet

/// <summary>The Onyx step's controls (setup spec §2.4, §5): Set up and Not now, and the login form once Onyx waits for it. The password lives in this object only until it is sent.</summary>
internal sealed class SetupOnyxViewModel : ObservableObject
{
    internal SetupOnyxViewModel(Func<Uri?> url, ISindriApi api, Func<string, bool> ready, Action<string> started, Action<string> said)
    {
        _ = (url, api, ready, started, said);
    }

    /// <summary>Raised when focus should move into the form.</summary>
    internal event EventHandler<SetupFocusEventArgs>? FocusRequested;

    /// <summary>The email field's label.</summary>
    public string EmailLabel => string.Empty;

    /// <summary>The password field's label.</summary>
    public string PasswordLabel => string.Empty;

    /// <summary>The line over the form.</summary>
    public string IntroText => string.Empty;

    /// <summary>The note under the form.</summary>
    public string NoteText => string.Empty;

    /// <summary>The email as typed.</summary>
    public string Email { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>The password as typed; cleared the moment it is sent.</summary>
    public string Password { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>True while the password shows as text.</summary>
    public bool IsRevealed { get; private set; }

    /// <summary>The mask character for the password field; '\0' while it shows.</summary>
    public char PasswordChar => '\0';

    /// <summary>"Show password" or "Hide password".</summary>
    public string RevealText => string.Empty;

    /// <summary>True when Set up Onyx can be pressed.</summary>
    public bool ShowStart => false;

    /// <summary>True when Not now can be pressed.</summary>
    public bool ShowDefer => false;

    /// <summary>True when the login form shows.</summary>
    public bool ShowForm => false;

    /// <summary>True when the form is closed and Connect opens it again.</summary>
    public bool ShowConnect => false;

    /// <summary>True while a press is on its way.</summary>
    public bool IsSending { get; private set; }

    /// <summary>An error shown under the email field.</summary>
    public string EmailError { get; private set; } = string.Empty;

    /// <summary>An error shown under the password field.</summary>
    public string PasswordError { get; private set; } = string.Empty;

    /// <summary>An error shown under the form.</summary>
    public string FormError { get; private set; } = string.Empty;

    /// <summary>Starts the Onyx stack once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Start(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sets Onyx aside for later, once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Defer(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends the login once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Connect(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Shows or hides the password text.</summary>
    internal void ToggleReveal() => throw new NotSupportedException();

    /// <summary>Closes the form (Esc): the password is forgotten and focus goes back to the Connect button.</summary>
    internal void Close() => throw new NotSupportedException();

    /// <summary>Opens the form again.</summary>
    internal void Open() => throw new NotSupportedException();

    /// <summary>Takes the step as served now.</summary>
    /// <param name="step">The step.</param>
    /// <param name="working">True while the row shows Working for a press the app sent.</param>
    internal void Update(SetupStepView step, bool working = false) => throw new NotSupportedException();

    /// <summary>Asks again which controls the catalog lets the app show.</summary>
    internal void Refresh() => throw new NotSupportedException();

    private void Unused() => FocusRequested?.Invoke(this, new SetupFocusEventArgs(SetupFocus.OnyxEmail));
}
