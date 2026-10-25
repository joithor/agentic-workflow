using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The Onyx step's controls (setup spec §2.4, §5): Set up and Not now, and the login form once Onyx waits for it. The password lives in this object only until it is sent.</summary>
internal sealed partial class SetupOnyxViewModel : ObservableObject
{
    private const string Todo = "todo";
    private const string Failed = "failed";
    private const string NeedsYou = "needs-you";
    private const string SetupAction = "SetupOnyx";
    private const string ConnectAction = "ConnectOnyx";
    private const string DeferAction = "DeferOnyx";
    private const string WrongLogin = "SND-ONYX-007";

    private static readonly string[] _all =
    [
        nameof(ShowStart), nameof(ShowDefer), nameof(ShowForm), nameof(ShowConnect), nameof(IsSending), nameof(EmailError), nameof(PasswordError), nameof(FormError),
        nameof(HasEmailError), nameof(HasPasswordError), nameof(HasFormError),
    ];

    private static readonly string[] _reveal = [nameof(IsRevealed), nameof(PasswordChar), nameof(RevealText)];

    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly Func<string, bool> _ready;
    private readonly Action<string> _started;
    private readonly Action<string> _said;
    private string _state = string.Empty;
    private bool _working;
    private bool _closed;

    internal SetupOnyxViewModel(Func<Uri?> url, ISindriApi api, Func<string, bool> ready, Action<string> started, Action<string> said)
    {
        (_url, _api, _ready, _started, _said) = (url, api, ready, started, said);
    }

    /// <summary>Raised when focus should move into the form.</summary>
    internal event EventHandler<SetupFocusEventArgs>? FocusRequested;

    /// <summary>The email as typed. Typing clears the error under it.</summary>
    public string Email
    {
        get;
        set
        {
            Set(ref field, value);
            Clear(email: true, password: false);
        }
    } = string.Empty;

    /// <summary>The password as typed; cleared the moment it is sent. Typing clears the error under it.</summary>
    public string Password
    {
        get;
        set
        {
            Set(ref field, value);
            Clear(email: false, password: true);
        }
    } = string.Empty;

    /// <summary>True while the password shows as text.</summary>
    public bool IsRevealed { get; private set; }

    /// <summary>The mask character for the password field; '\0' while it shows.</summary>
    public char PasswordChar => IsRevealed ? '\0' : '•';

    /// <summary>"Show password" or "Hide password".</summary>
    public string RevealText => IsRevealed ? Labels.SetupOnyxHide : Labels.SetupOnyxShow;

    /// <summary>True when Set up Onyx can be pressed.</summary>
    public bool ShowStart => !_working && !IsSending && (string.Equals(_state, Todo, StringComparison.Ordinal) || string.Equals(_state, Failed, StringComparison.Ordinal)) && _ready(SetupAction);

    /// <summary>True when Not now can be pressed.</summary>
    public bool ShowDefer => !_working && !IsSending && string.Equals(_state, Todo, StringComparison.Ordinal) && _ready(DeferAction);

    /// <summary>True when the login form shows.</summary>
    public bool ShowForm => Waiting && !_closed;

    /// <summary>True when the form is closed and Connect opens it again.</summary>
    public bool ShowConnect => Waiting && _closed;

    /// <summary>True while a press is on its way.</summary>
    public bool IsSending { get; private set; }

    /// <summary>An error shown under the email field.</summary>
    public string EmailError { get; private set; } = string.Empty;

    /// <summary>An error shown under the password field.</summary>
    public string PasswordError { get; private set; } = string.Empty;

    /// <summary>An error shown under the form.</summary>
    public string FormError { get; private set; } = string.Empty;

    // The login waits for the person, the app may send it, and no retry of the row is on its way.
    private bool Waiting => !_working && string.Equals(_state, NeedsYou, StringComparison.Ordinal) && _ready(ConnectAction);

    /// <summary>Starts the Onyx stack once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Start(CancellationToken cancellationToken)
    {
        if (!ShowStart || _url() is not { } url)
        {
            return;
        }

        Sending(on: true);
        Result<JobStarted> started;
        try
        {
            started = await _api.SetupOnyx(url, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Sending(on: false);
        }

        if (started.IsSuccess)
        {
            _started(Labels.SetupOnyxStarting);
        }
        else
        {
            _said(Labels.SetupRefusal(started.Error.Code) ?? Labels.SetupOnyxFailed);
        }
    }

    /// <summary>Sets Onyx aside for later, once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Defer(CancellationToken cancellationToken)
    {
        if (!ShowDefer || _url() is not { } url)
        {
            return;
        }

        Sending(on: true);
        Result<OnyxDeferView> deferred;
        try
        {
            deferred = await _api.DeferOnyx(url, new OnyxDeferRequest(Deferred: true), cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Sending(on: false);
        }

        _said(deferred.IsSuccess ? Labels.SetupOnyxLater : Labels.SetupRefusal(deferred.Error.Code) ?? Labels.SetupOnyxFailed);
    }

    /// <summary>Sends the login once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Connect(CancellationToken cancellationToken)
    {
        if (IsSending || _url() is not { } url)
        {
            return;
        }

        var email = Email.Trim();
        if (email.Length == 0 || Password.Length == 0)
        {
            Need(email.Length == 0, Password.Length == 0);
            return;
        }

        var login = new OnyxLogin(email, Password);
        Password = string.Empty;   // the password leaves this object the moment it is sent
        Hide();
        Sending(on: true);
        Result<JobStarted> started;
        try
        {
            started = await _api.ConnectOnyx(url, login, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Sending(on: false);
        }

        if (started.IsSuccess)
        {
            _started(Labels.SetupOnyxConnecting);
        }
        else
        {
            Refuse(started.Error.Code);
        }
    }

    /// <summary>Shows or hides the password text.</summary>
    internal void ToggleReveal()
    {
        IsRevealed = !IsRevealed;
        Notify(_reveal);
    }

    /// <summary>Closes the form (Esc): the password is forgotten and focus goes back to the Connect button.</summary>
    internal void Close()
    {
        Password = string.Empty;
        Hide();
        _closed = true;
        Notify(_all);
        FocusRequested?.Invoke(this, new SetupFocusEventArgs(SetupFocus.OnyxConnect));
    }

    /// <summary>Opens the form again.</summary>
    internal void Open()
    {
        _closed = false;
        Notify(_all);
        FocusRequested?.Invoke(this, new SetupFocusEventArgs(SetupFocus.OnyxEmail));
    }

    /// <summary>Takes the step as served now.</summary>
    /// <param name="step">The step.</param>
    /// <param name="working">True while the row shows Working for a press the app sent.</param>
    internal void Update(SetupStepView step, bool working = false)
    {
        var was = ShowForm;
        _state = step.State;
        _working = working;
        if (!Waiting)
        {
            _closed = false;
        }

        Notify(_all);
        if (!was && ShowForm)
        {
            FocusRequested?.Invoke(this, new SetupFocusEventArgs(SetupFocus.OnyxEmail));
        }
    }

    /// <summary>Asks again which controls the catalog lets the app show.</summary>
    internal void Refresh() => Notify(_all);

    private void Hide()
    {
        IsRevealed = false;
        Notify(_reveal);
    }

    private void Need(bool email, bool password)
    {
        (EmailError, PasswordError, FormError) = (email ? Labels.SetupOnyxEmailNeeded : string.Empty, password ? Labels.SetupOnyxPasswordNeeded : string.Empty, string.Empty);
        Notify(_all);
        FocusRequested?.Invoke(this, new SetupFocusEventArgs(email ? SetupFocus.OnyxEmail : SetupFocus.OnyxPassword));
    }

    // Where a refusal belongs: a wrong login under the password, anything else under the form.
    private void Refuse(string code)
    {
        var wrong = string.Equals(code, WrongLogin, StringComparison.Ordinal);
        var sentence = Labels.SetupRefusal(code) ?? Labels.SetupOnyxFailed;
        (EmailError, PasswordError, FormError) = (string.Empty, wrong ? sentence : string.Empty, wrong ? string.Empty : sentence);
        Notify(_all);
        FocusRequested?.Invoke(this, new SetupFocusEventArgs(wrong ? SetupFocus.OnyxPassword : SetupFocus.OnyxEmail));
    }

    private void Clear(bool email, bool password)
    {
        if (!((email && EmailError.Length > 0) || (password && PasswordError.Length > 0) || FormError.Length > 0))
        {
            return;
        }

        (EmailError, PasswordError, FormError) = (email ? string.Empty : EmailError, password ? string.Empty : PasswordError, string.Empty);
        Notify(_all);
    }

    private void Sending(bool on)
    {
        IsSending = on;
        Notify(_all);
    }

    private void Notify(string[] names)
    {
        foreach (var name in names)
        {
            OnChanged(name);
        }
    }
}
