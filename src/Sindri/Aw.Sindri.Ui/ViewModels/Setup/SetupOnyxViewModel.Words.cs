namespace Aw.Sindri.Ui.ViewModels.Setup;

#pragma warning disable CA1822, MA0041 // why: the view binds these as instance properties of the view model
internal sealed partial class SetupOnyxViewModel
{
    /// <summary>The email field's label.</summary>
    public string EmailLabel => Labels.SetupOnyxEmail;

    /// <summary>The password field's label.</summary>
    public string PasswordLabel => Labels.SetupOnyxPassword;

    /// <summary>The line over the form.</summary>
    public string IntroText => Labels.SetupOnyxIntro;

    /// <summary>The note under the form.</summary>
    public string NoteText => Labels.SetupOnyxNote;

    /// <summary>Set up Onyx's words.</summary>
    public string StartText => Labels.SetupActionLabel(SetupAction, done: false);

    /// <summary>Not now's words.</summary>
    public string DeferText => Labels.SetupOnyxNotNow;

    /// <summary>Connect's words (it sends the login, and opens the form again).</summary>
    public string ConnectText => Labels.SetupActConnect;

    /// <summary>True when there is an email error.</summary>
    public bool HasEmailError => EmailError.Length > 0;

    /// <summary>True when there is a password error.</summary>
    public bool HasPasswordError => PasswordError.Length > 0;

    /// <summary>True when there is a form error.</summary>
    public bool HasFormError => FormError.Length > 0;
}
