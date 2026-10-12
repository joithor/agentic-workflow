namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Health page's status card (<see cref="HealthViewModel"/>, continued): the lease, the API, the profile, the services.</summary>
internal sealed partial class HealthViewModel
{
    /// <summary>The polite announcement while a refresh is in flight (§4.1 "sending"): "Sending…" after 300 ms, else empty.</summary>
    public string SendingAnnouncement { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Not running: the endpoint record's path, shortened, shown on its own line under the reason; or empty.</summary>
    public string ReasonPath { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The command that fixes "not running".</summary>
    public string Fix { get; } = Labels.StartFix;

    /// <summary>Running: the lease line.</summary>
    public string Lease { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>Where the API listens.</summary>
    public string Api { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's state in words.</summary>
    public string ProfileState { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The profile's hash and file.</summary>
    public string ProfileDetail { get; private set => Set(ref field, value); } = string.Empty;

    /// <summary>The command that fixes the profile's state, or empty.</summary>
    public string ProfileFix
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasProfileFix));
        }
    } = string.Empty;

    /// <summary>True when the profile needs a command.</summary>
    public bool HasProfileFix => ProfileFix.Length > 0;

    /// <summary>The scheduled services.</summary>
    public IReadOnlyList<ServiceRow> Services
    {
        get;
        private set
        {
            Set(ref field, value);
            OnChanged(nameof(HasServices));
        }
    } = [];

    /// <summary>True when any service is scheduled.</summary>
    public bool HasServices => Services.Count > 0;
}
