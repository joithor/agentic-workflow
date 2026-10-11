namespace Aw.Sindri.Core.Events;

/// <summary>The event types S1 records.</summary>
public static class EventTypes
{
    /// <summary>The control plane took the lease and started.</summary>
    public const string ProcessStarted = "process.started";

    /// <summary>The control plane released the lease and stopped.</summary>
    public const string ProcessStopped = "process.stopped";

    /// <summary>A human approved a profile hash.</summary>
    public const string ProfileApproved = "profile.approved";

    /// <summary>The heartbeat service ran.</summary>
    public const string Heartbeat = "service.heartbeat";
}
