using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Events;

/// <summary>The event types Sindri records (each one is in EventCatalog).</summary>
public static class EventTypes
{
    /// <summary>The control plane took the lease and started.</summary>
    public const string ProcessStarted = "process.started";

    /// <summary>The control plane released the lease and stopped.</summary>
    public const string ProcessStopped = "process.stopped";

    /// <summary>A recipe instance started (mirrored from its RecipeWorkflow; engine spec rev 8.1 §5.6).</summary>
    public const string WorkflowStarted = "workflow.started";

    /// <summary>A recipe instance moved from one state to another (mirrored from its RecipeWorkflow).</summary>
    public const string WorkflowTransition = "workflow.transition";

    /// <summary>A human approved a profile hash.</summary>
    public const string ProfileApproved = "profile.approved";

    /// <summary>The context engine refreshed its snapshot sources (context spec §3; the context-docs-refresh service).</summary>
    public const string ContextSourcesSynced = "context.sources.synced";

    /// <summary>A command attempt reached the chain: its audit row (not public).</summary>
    public const string CommandHandled = "command.handled";

    /// <summary>A background service ran a slot, ok or failed (API spec C8).</summary>
    public const string ServiceRan = "service.ran";

    /// <summary>The profile watch saw the (approved, live) pair change (API spec C8).</summary>
    public const string ProfileChecked = "profile.checked";

    /// <summary>A human asked for the views to be rebuilt (not public).</summary>
    public const string ViewsRebuilt = "views.rebuilt";

    /// <summary>The event a background service records per run: 'service.&lt;name&gt;'.</summary>
    /// <param name="service">The service.</param>
    /// <returns>The event type, for example 'service.heartbeat'.</returns>
    public static string ServiceRun(ServiceName service) => "service." + service.Value;
}
