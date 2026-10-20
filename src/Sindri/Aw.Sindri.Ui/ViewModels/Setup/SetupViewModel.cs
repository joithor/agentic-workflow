#pragma warning disable MA0041 // red-phase stub: members throw until the implementation commit
using System.Collections.ObjectModel;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.ViewModels.Controls;

namespace Aw.Sindri.Ui.ViewModels.Setup;

/// <summary>The Setup list behind the card on Home and the Setup page (setup-once spec §5.7).</summary>
internal sealed class SetupViewModel : ObservableObject
{
    private Uri? _url;

    internal SetupViewModel(ISindriApi api, CatalogViewModel catalog, TimeProvider clock, Func<string, bool> opensInApp)
    {
        _ = (api, catalog, clock, opensInApp);
    }

    /// <summary>Raised when a step's button asks the shell to open an action.</summary>
    internal event EventHandler<SetupActionEventArgs>? ActionRequested;

    /// <summary>The steps in the order served.</summary>
    public ObservableCollection<SetupStepViewModel> Steps { get; } = [];

    /// <summary>The rows the card draws.</summary>
    public ObservableCollection<SetupStepViewModel> CardSteps { get; } = [];

    /// <summary>The step the page leads with; null when setup is finished.</summary>
    public SetupStepViewModel? Next => throw new NotSupportedException();

    /// <summary>The ids of the steps the card draws.</summary>
    internal IReadOnlySet<string> DrawnIds => throw new NotSupportedException();

    /// <summary>The polite live region.</summary>
    public LiveAnnouncer Live { get; } = new(Labels.SetupPage);

    public string Head => throw new NotSupportedException();

    public string LeadText => throw new NotSupportedException();

    public string MoreText => throw new NotSupportedException();

    public bool HasMore => throw new NotSupportedException();

    public bool IsExpanded => throw new NotSupportedException();

    public bool IsFinished => throw new NotSupportedException();

    public bool IsCardVisible => throw new NotSupportedException();

    public bool IsReading => throw new NotSupportedException();

    public bool IsNotServed => throw new NotSupportedException();

    public string NotServedText => throw new NotSupportedException();

    public bool IsUnreadable => throw new NotSupportedException();

    public bool IsStale => throw new NotSupportedException();

    public bool IsFailed => throw new NotSupportedException();

    public string FailedText => throw new NotSupportedException();

    public bool IsNotRunning => throw new NotSupportedException();

    public string NotRunningText => throw new NotSupportedException();

    public string ReconnectsText => throw new NotSupportedException();

    public ControlViewModel? Launcher => throw new NotSupportedException();

    internal void ToggleMore() => throw new NotSupportedException();

    internal void RequestOpenPage() => throw new NotSupportedException();

    internal Task Refresh(Uri url, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _url = url;
        ActionRequested?.Invoke(this, new SetupActionEventArgs(url.Host));
        _ = new SetupStepViewModel(new Api.Setup.SetupStepView(url.Host, string.Empty, string.Empty, string.Empty, Detail: null, Error: null, Action: null, Progress: null)).Update(new Api.Setup.SetupStepView(url.Host, string.Empty, string.Empty, string.Empty, Detail: null, Error: null, Action: null, Progress: null));
        throw new NotSupportedException();
    }

    internal Task Retry(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = _url;
        throw new NotSupportedException();
    }
}
