namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The app's own copy (<see cref="Labels"/>) as the views bind it: compiled bindings resolve public members only, and the
/// labels stay internal, so each view model hands its view this instance as its Text.
/// </summary>
internal sealed class ShellText
{
    /// <summary>The one instance.</summary>
    internal static ShellText Instance { get; } = new();

    /// <summary><see cref="Labels.Back"/>.</summary>
    public string Back { get; } = Labels.Back;

    /// <summary><see cref="Labels.CheckIt"/>.</summary>
    public string CheckIt { get; } = Labels.CheckIt;

    /// <summary><see cref="Labels.CloseSheet"/>.</summary>
    public string CloseSheet { get; } = Labels.CloseSheet;

    /// <summary><see cref="Labels.Connecting"/>.</summary>
    public string Connecting { get; } = Labels.Connecting;

    /// <summary><see cref="Labels.ControlPlaneCard"/>.</summary>
    public string ControlPlaneCard { get; } = Labels.ControlPlaneCard;

    /// <summary><see cref="Labels.Decisions"/>.</summary>
    public string Decisions { get; } = Labels.Decisions;

    /// <summary><see cref="Labels.DoctorFix"/>.</summary>
    public string DoctorFix { get; } = Labels.DoctorFix;

    /// <summary><see cref="Labels.Health"/>.</summary>
    public string Health { get; } = Labels.Health;

    /// <summary><see cref="Labels.HealthPanel"/>.</summary>
    public string HealthPanel { get; } = Labels.HealthPanel;

    /// <summary><see cref="Labels.Home"/>.</summary>
    public string Home { get; } = Labels.Home;

    /// <summary><see cref="Labels.LastEventCard"/>.</summary>
    public string LastEventCard { get; } = Labels.LastEventCard;

    /// <summary><see cref="Labels.Later"/>.</summary>
    public string Later { get; } = Labels.Later;

    /// <summary><see cref="Labels.LeaseCard"/>.</summary>
    public string LeaseCard { get; } = Labels.LeaseCard;

    /// <summary><see cref="Labels.NeedsYouPanel"/>.</summary>
    public string NeedsYouPanel { get; } = Labels.NeedsYouPanel;

    /// <summary><see cref="Labels.NoServices"/>.</summary>
    public string NoServices { get; } = Labels.NoServices;

    /// <summary><see cref="Labels.NotRunning"/>.</summary>
    public string NotRunning { get; } = Labels.NotRunning;

    /// <summary><see cref="Labels.NotServedYet"/>.</summary>
    public string NotServedYet { get; } = Labels.NotServedYet;

    /// <summary><see cref="Labels.Pages"/>.</summary>
    public string Pages { get; } = Labels.Pages;

    /// <summary><see cref="Labels.ProfileCard"/>.</summary>
    public string ProfileCard { get; } = Labels.ProfileCard;

    /// <summary><see cref="Labels.Queue"/>.</summary>
    public string Queue { get; } = Labels.Queue;

    /// <summary><see cref="Labels.QueuedPanel"/>.</summary>
    public string QueuedPanel { get; } = Labels.QueuedPanel;

    /// <summary><see cref="Labels.Refresh"/>.</summary>
    public string Refresh { get; } = Labels.Refresh;

    /// <summary><see cref="Labels.RunningPanel"/>.</summary>
    public string RunningPanel { get; } = Labels.RunningPanel;

    /// <summary><see cref="Labels.Search"/>.</summary>
    public string Search { get; } = Labels.Search;

    /// <summary><see cref="Labels.SearchKey"/>.</summary>
    public string SearchKey { get; } = Labels.SearchKey;

    /// <summary><see cref="Labels.Services"/>.</summary>
    public string Services { get; } = Labels.Services;

    /// <summary><see cref="Labels.ServicesCard"/>.</summary>
    public string ServicesCard { get; } = Labels.ServicesCard;

    /// <summary><see cref="Labels.Settings"/>.</summary>
    public string Settings { get; } = Labels.Settings;

    /// <summary><see cref="Labels.Sheet"/>.</summary>
    public string Sheet { get; } = Labels.Sheet;

    /// <summary><see cref="Labels.Sindri"/>.</summary>
    public string Sindri { get; } = Labels.Sindri;

    /// <summary><see cref="Labels.StartFix"/>.</summary>
    public string StartFix { get; } = Labels.StartFix;

    /// <summary><see cref="Labels.StartIt"/>.</summary>
    public string StartIt { get; } = Labels.StartIt;

    /// <summary><see cref="Labels.Unreadable"/>.</summary>
    public string Unreadable { get; } = Labels.Unreadable;

    /// <summary><see cref="Labels.VersionMismatch"/>.</summary>
    public string VersionMismatch { get; } = Labels.VersionMismatch;

    /// <summary><see cref="Labels.VersionMismatchFix"/>.</summary>
    public string VersionMismatchFix { get; } = Labels.VersionMismatchFix;
}
