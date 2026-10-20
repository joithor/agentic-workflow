namespace Aw.Sindri.Ui.Api;

/// <summary>
/// A 'workflow.instance-changed' frame (API spec §5): an instance's served view changed, so read 'GET /workflow/instances/{id}' again.
/// The frame has no "id:" line, so it never moves the event cursor.
/// </summary>
/// <param name="Instance">The instance address.</param>
/// <param name="State">The state after the change; the authority for whether a re-read has caught up (the view is read live and can lag).</param>
/// <param name="Cursor">The ledger sequence of the change (not comparable with the view, which carries none).</param>
internal sealed record InstanceChange(string Instance, string State, long Cursor)
{
    /// <summary>The stream type, and the name of the frame's event.</summary>
    internal const string Type = "workflow.instance-changed";
}
