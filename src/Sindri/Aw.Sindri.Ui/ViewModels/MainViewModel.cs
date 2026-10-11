using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The main window: the control plane's status, kept live from its event stream.</summary>
/// <param name="records">Where the control plane recorded its endpoint.</param>
/// <param name="api">Its API.</param>
/// <param name="clock">The clock for retry waits.</param>
internal sealed class MainViewModel(IEndpointRecordSource records, ISindriApi api, TimeProvider clock)
{
    /// <summary>How long to wait before looking for the control plane again.</summary>
    internal static readonly TimeSpan RetryEvery = TimeSpan.FromSeconds(2);

    /// <summary>The state in one word: Connecting, Running or Not running.</summary>
    public string Headline => Red<string>();

    /// <summary>Why it isn't running (when it isn't).</summary>
    public string Reason => Red<string>();

    /// <summary>The command that fixes "not running".</summary>
    public string Fix => Red<string>();

    /// <summary>Running: the lease line.</summary>
    public string Lease => Red<string>();

    /// <summary>The profile's state in words.</summary>
    public string ProfileState => Red<string>();

    /// <summary>The profile's hash and file.</summary>
    public string ProfileDetail => Red<string>();

    /// <summary>The command that fixes the profile's state, or empty.</summary>
    public string ProfileFix => Red<string>();

    /// <summary>The scheduled services.</summary>
    public IReadOnlyList<ServiceRow> Services => Red<IReadOnlyList<ServiceRow>>();

    /// <summary>The newest event seen on the stream.</summary>
    public string LastEvent => Red<string>();

    /// <summary>True while a control plane answers.</summary>
    public bool IsRunning => Red<bool>();

    /// <summary>True once it's known that none does.</summary>
    public bool IsNotRunning => Red<bool>();

    /// <summary>Where the API listens.</summary>
    public string Api => Red<string>();

    /// <summary>True when the profile needs a command.</summary>
    public bool HasProfileFix => Red<bool>();

    /// <summary>True when any service is scheduled.</summary>
    public bool HasServices => Red<bool>();

    /// <summary>Follows the control plane until cancelled.</summary>
    /// <param name="cancellationToken">Stops following.</param>
    /// <returns>A task that ends when cancelled.</returns>
    internal Task Run(CancellationToken cancellationToken) => cancellationToken.IsCancellationRequested ? Task.CompletedTask : Red<Task>();

    private T Red<T>() => throw new NotSupportedException($"red {records}{api}{clock}");
}
