using Aw.Kernel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The repo step's form (setup spec §2.4, §5): a folder to type or Browse for, and the button that adds it. It shows only while the catalog lets the app add a repo; until then the step is the command to copy.</summary>
internal sealed class SetupRepoFormViewModel : ObservableObject
{
    private static readonly string[] _all = [nameof(IsAvailable), nameof(CanBrowse), nameof(CanAdd), nameof(AddText), nameof(IsSending), nameof(ErrorText), nameof(HasError)];

    private readonly Func<Uri?> _url;
    private readonly ISindriApi _api;
    private readonly IFolderPicker _folders;
    private readonly Func<bool> _ready;
    private readonly Action<string> _started;
    private bool _done;

    internal SetupRepoFormViewModel(Func<Uri?> url, ISindriApi api, IFolderPicker folders, Func<bool> ready, Action<string> started)
    {
        (_url, _api, _folders, _ready, _started) = (url, api, folders, ready, started);
    }

    /// <summary>Raised when focus should move into the form.</summary>
    internal event EventHandler<SetupFocusEventArgs>? FocusRequested;

    /// <summary>The folder as typed or chosen. Typing clears the error under the field.</summary>
    public string Path
    {
        get;
        set
        {
            Set(ref field, value);
            if (ErrorText.Length > 0)
            {
                ErrorText = string.Empty;
                Notify(nameof(ErrorText), nameof(HasError));
            }

            Notify(nameof(CanAdd));
        }
    } = string.Empty;

#pragma warning disable CA1822, MA0041 // why: the view binds these as instance properties of the view model
    /// <summary>The field's label.</summary>
    public string FolderLabel => Labels.SetupFolderLabel;

    /// <summary>The chooser button's words.</summary>
    public string BrowseText => Labels.SetupBrowse;

    /// <summary>The line over the form.</summary>
    public string BodyText => Labels.SetupRepoBody;

    /// <summary>The note under the form.</summary>
    public string NoteText => Labels.SetupRepoNote;

#pragma warning restore CA1822, MA0041
    /// <summary>True when the app adds the repo itself (the catalog says so).</summary>
    public bool IsAvailable => _ready();

    /// <summary>True when this head can show a folder chooser.</summary>
    public bool CanBrowse => _folders.IsAvailable;

    /// <summary>True while the add is on its way.</summary>
    public bool IsSending { get; private set; }

    /// <summary>True when the Add button can be pressed.</summary>
    public bool CanAdd => IsAvailable && !IsSending && Path.Trim().Length > 0;

    /// <summary>What is wrong with the folder, in words, shown under the field.</summary>
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>True when there is an error to show.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The Add button's words: another repo once one is added.</summary>
    public string AddText => Labels.SetupActionLabel("AddRepo", _done);

    /// <summary>Opens the folder chooser and puts the chosen folder in the field; no choice leaves what was typed.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the field is set.</returns>
    internal async Task Browse(CancellationToken cancellationToken)
    {
        var chosen = await _folders.Pick(Labels.SetupBrowseTitle, cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            Path = chosen;
        }
    }

    /// <summary>Sends the add once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal async Task Add(CancellationToken cancellationToken)
    {
        if (IsSending || _url() is not { } url)
        {
            return;
        }

        var path = Path.Trim();
        if (path.Length == 0)
        {
            Refuse(Labels.SetupRepoEmpty);
            return;
        }

        Sending(on: true);
        Result<JobStarted> started;
        try
        {
            started = await _api.AddRepo(url, new AddRepoRequest(path), cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            Sending(on: false);
        }

        if (started.IsSuccess)
        {
            Path = string.Empty;
            _started(Labels.SetupRepoAdding);
        }
        else
        {
            Refuse(Labels.SetupRefusal(started.Error.Code) ?? Labels.SetupRepoFailed);
        }
    }

    /// <summary>Takes the step as served now.</summary>
    /// <param name="step">The step.</param>
    internal void Update(SetupStepView step)
    {
        _done = string.Equals(step.State, "done", StringComparison.Ordinal);
        Notify(_all);
    }

    /// <summary>Asks again whether the app can add a repo now (the catalog changed).</summary>
    internal void Refresh() => Notify(_all);

    private void Refuse(string sentence)
    {
        ErrorText = sentence;
        Notify(nameof(ErrorText), nameof(HasError));
        FocusRequested?.Invoke(this, new SetupFocusEventArgs(SetupFocus.RepoPath));
    }

    private void Sending(bool on)
    {
        IsSending = on;
        Notify(nameof(IsSending), nameof(CanAdd));
    }

    private void Notify(params string[] names)
    {
        foreach (var name in names)
        {
            OnChanged(name);
        }
    }
}
