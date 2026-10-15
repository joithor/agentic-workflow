using Aw.Sindri.Core.Workflow.Registry;

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
        new(EventTypes.ContextSourcesSynced, Public: true, typeof(ContextSourcesSynced), "context.sources.synced:<epoch>:<slot>"),
        new(EventTypes.ContextMapImported, Public: true, typeof(ContextMapImported), "context.map.imported:<hash>:v<version>"),
        new(EventTypes.CommandHandled, Public: false, PublicPayload: null, "command.handled:<commandId>"),
        new(EventTypes.RecipeRegistered, Public: true, typeof(RecipeRegistered), "recipe.registered:<hash>"),
        new(EventTypes.RecipeApproved, Public: true, typeof(RecipeApproved), "recipe.approved:<hash>:after:<seq>"),
        new(EventTypes.RecipeRevoked, Public: true, typeof(RecipeRevoked), "recipe.revoked:<hash>"),
        new(EventTypes.WorkflowStarted, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowTransition, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowRefused, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowShifted, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowNotified, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.WorkflowExpired, Public: true, typeof(WorkflowEventPayload), "workflow:<workflow id>:<run id>:<activity id>[:<content hash>]"),
        new(EventTypes.ServiceRan, Public: true, typeof(ServiceRan), "service.ran:<name>:<epoch>:<slot>"),
        new(EventTypes.ProfileChecked, Public: true, typeof(ProfileChecked), "profile.checked:<previousCheckSequence>"),
        new(EventTypes.ViewsRebuilt, Public: false, PublicPayload: null, "views.rebuilt:<commandId>"),
    ];
}
