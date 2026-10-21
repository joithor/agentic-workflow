using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Setup;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels.Setup;

#pragma warning disable CA1822, MA0041, IDE0051 // the red commit's stubs: no behaviour yet

/// <summary>The repo step's form (setup spec §2.4, §5): a folder to type or Browse for, and the button that adds it.</summary>
internal sealed class SetupRepoFormViewModel : ObservableObject
{
    internal SetupRepoFormViewModel(Func<Uri?> url, ISindriApi api, IFolderPicker folders, Func<bool> ready, Action<string> started)
    {
        _ = (url, api, folders, ready, started);
    }

    /// <summary>Raised when focus should move into the form.</summary>
    internal event EventHandler<SetupFocusEventArgs>? FocusRequested;

    /// <summary>The folder as typed or chosen.</summary>
    public string Path { get; set => Set(ref field, value); } = string.Empty;

    /// <summary>True when the app adds the repo itself (the catalog says so).</summary>
    public bool IsAvailable => false;

    /// <summary>True when this head can show a folder chooser.</summary>
    public bool CanBrowse => false;

    /// <summary>True while the add is on its way.</summary>
    public bool IsSending { get; private set; }

    /// <summary>True when the Add button can be pressed.</summary>
    public bool CanAdd => false;

    /// <summary>What is wrong with the folder, in words, shown under the field.</summary>
    public string ErrorText { get; private set; } = string.Empty;

    /// <summary>True when there is an error to show.</summary>
    public bool HasError => ErrorText.Length > 0;

    /// <summary>The Add button's words.</summary>
    public string AddText => string.Empty;

    /// <summary>Opens the folder chooser and puts the chosen folder in the field.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A task that completes when the field is set.</returns>
    internal Task Browse(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Sends the add once.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the answer is shown.</returns>
    internal Task Add(CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <summary>Takes the step as served now.</summary>
    /// <param name="step">The step.</param>
    internal void Update(SetupStepView step) => throw new NotSupportedException();

    /// <summary>Asks again whether the app can add a repo now (the catalog changed).</summary>
    internal void Refresh() => throw new NotSupportedException();

    private void Unused() => FocusRequested?.Invoke(this, new SetupFocusEventArgs(SetupFocus.RepoPath));
}
