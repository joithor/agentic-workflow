namespace Aw.Sindri.Core.Events;

/// <summary>Every event type Sindri records (API spec §3.4): the phase that adds an event registers it here.</summary>
public static class EventCatalog
{
    /// <summary>Every entry.</summary>
    public static IReadOnlyList<EventCatalogEntry> All { get; } =
    [
        // one row per event type; phases append
        new(EventTypes.ProcessStarted, Public: true, typeof(ProcessStarted), "process.started:<epoch>"),
        new(EventTypes.ProcessStopped, Public: true, typeof(ProcessStopped), "process.stopped:<epoch>"),
        new(EventTypes.ProfileApproved, Public: true, typeof(ProfileApproved), "profile.approved:<hash>:after:<seq>"),
        new("service.heartbeat", Public: true, typeof(Heartbeat), "service.heartbeat:<epoch>:<slot>"),
        new(EventTypes.CommandHandled, Public: false, PublicPayload: null, "command.handled:<commandId>"),
        new(EventTypes.ServiceRan, Public: true, typeof(ServiceRan), "service.ran:<name>:<epoch>:<slot>"),
        new(EventTypes.ProfileChecked, Public: true, typeof(ProfileChecked), "profile.checked:<previousCheckSequence>"),
        new(EventTypes.ViewsRebuilt, Public: false, PublicPayload: null, "views.rebuilt:<commandId>"),
    ];
}
