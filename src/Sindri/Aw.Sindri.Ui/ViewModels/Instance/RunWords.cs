using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// An agent run in words: the launch's state, why Attach waits, and what happened to the agent's Steps. Every sentence is a
/// served field mapped to the app's own copy; a code or a detail is shown as served.
/// </summary>
internal static class RunWords
{
    private const string Print = "print";

    /// <summary>The launch's state: requested, started (with what its viewer did) or failed (with its code and detail).</summary>
    /// <param name="launch">The launch as served.</param>
    /// <returns>The line.</returns>
    internal static string LaunchLine(LaunchView launch)
    {
        ArgumentNullException.ThrowIfNull(launch);
        if (string.Equals(launch.State, "failed", StringComparison.Ordinal))
        {
            return Labels.LaunchFailed(launch.Code ?? "?", launch.Detail ?? string.Empty);
        }

        if (!string.Equals(launch.State, "started", StringComparison.Ordinal))
        {
            return Labels.LaunchRequested;
        }

        return launch.Viewer is not { } viewer ? Labels.LaunchRunning : ViewerLine(viewer);
    }

    /// <summary>What a started launch's viewer did, in words: opened, unconfirmed, failed (code and detail) or skipped (the served reason).</summary>
    /// <param name="viewer">The viewer as served.</param>
    /// <returns>The line.</returns>
    internal static string ViewerLine(LaunchViewerView viewer)
    {
        ArgumentNullException.ThrowIfNull(viewer);
        return viewer.Outcome switch
        {
            "opened" => Labels.LaunchOpenedIn(viewer.Label ?? viewer.Kind),
            "unconfirmed" => Labels.LaunchUnconfirmed(viewer.Label ?? viewer.Kind),
            "failed" => Labels.LaunchViewerFailed(viewer.Code ?? "?", viewer.Detail ?? string.Empty),
            _ => viewer.Reason is { Length: > 0 } reason ? Labels.LaunchViewerSkipped(reason) : Labels.LaunchRunning,
        };
    }

    /// <summary>True when the launch carries an attach command to show (no viewer opened the terminal).</summary>
    /// <param name="launch">The launch as served.</param>
    /// <returns>True when the command is shown.</returns>
    internal static bool ShowsCommand(LaunchView launch) => ShowsCommand(launch.Viewer, launch.Attach);

    /// <summary>True when there is an attach command and no viewer opened or may have opened the terminal: no viewer, the print viewer, or one that failed or was skipped.</summary>
    /// <param name="viewer">The viewer as served, or null.</param>
    /// <param name="attach">The attach command as served, or null.</param>
    /// <returns>True when the command is shown.</returns>
    internal static bool ShowsCommand(LaunchViewerView? viewer, IReadOnlyList<string>? attach) =>
        attach is { Count: > 0 }
        && (viewer is null
            || string.Equals(viewer.Kind, Print, StringComparison.Ordinal)
            || string.Equals(viewer.Outcome, "failed", StringComparison.Ordinal)
            || string.Equals(viewer.Outcome, "skipped", StringComparison.Ordinal));

    /// <summary>What a takeover's viewer came to: the line, and the attach command when the person must run it.</summary>
    /// <param name="viewer">The viewer as served, or null when none was served.</param>
    /// <param name="attach">The attach command as served, or null.</param>
    /// <returns>The line and the command (empty when none is shown).</returns>
    internal static (string Line, string Command) TakeoverOutcome(LaunchViewerView? viewer, IReadOnlyList<string>? attach)
    {
        var command = ShowsCommand(viewer, attach) ? string.Join(' ', attach!) : string.Empty;   // why: ShowsCommand is true only when attach is non-empty
        var line = viewer is null ? (command.Length > 0 ? Labels.AttachRunThis : Labels.TakeoverNoViewer)
            : command.Length > 0 && string.Equals(viewer.Kind, Print, StringComparison.Ordinal) ? Labels.AttachRunThis
            : ViewerLine(viewer);
        return (line, command);
    }

    /// <summary>The attach command as one line.</summary>
    /// <param name="launch">The launch as served.</param>
    /// <returns>The line, or empty.</returns>
    internal static string Command(LaunchView launch) => launch.Attach is { } argv ? string.Join(' ', argv) : string.Empty;

    /// <summary>True when the item serves an Attach: the mode is offered and a live session is named.</summary>
    /// <param name="launch">The item's launch block, or null.</param>
    /// <returns>True when ready.</returns>
    internal static bool AttachReady(ItemLaunchView? launch) =>
        launch is { Attach: not null } && launch.Modes.Contains("attach", StringComparer.Ordinal);

    /// <summary>Why Attach is not ready, from what the item serves; null when it is ready.</summary>
    /// <param name="item">The item as read, or null when not read.</param>
    /// <param name="unreadable">True when the read failed (as opposed to not made yet).</param>
    /// <returns>The reason, or null.</returns>
    internal static string? PendingReason(ItemView? item, bool unreadable)
    {
        if (item is null)
        {
            return unreadable ? Labels.AttachItemUnreadable : Labels.AttachChecking;
        }

        var launch = item.Launch;
        if (AttachReady(launch))
        {
            return null;
        }

        return launch is null ? Labels.AttachItemUnreadable
            : launch.InFlight is not null ? Labels.AttachLaunchStarting
            : launch.Blockers.Count > 0 ? launch.Blockers[0].Detail
            : Labels.AttachNoLiveSession;
    }

    /// <summary>What an agent's Steps came to, newest last: "via agent · N tokens" for a completion, a plain sentence for a failure.</summary>
    /// <param name="events">The instance's events.</param>
    /// <returns>The lines.</returns>
    internal static IReadOnlyList<string> EventLines(IReadOnlyList<InstanceEventRow> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var lines = new List<string>();
        foreach (var fields in events.Select(e => e.Transition).OfType<InstanceEventFields>())
        {
            if (string.Equals(fields.Trigger, "step.failed", StringComparison.Ordinal) && (fields.Via is null || string.Equals(fields.Via, "agent", StringComparison.Ordinal)) && fields.Reason is not null)
            {
                lines.Add(Labels.AgentFailed(fields.Reason, fields.To));
            }
            else if (string.Equals(fields.Via, "agent", StringComparison.Ordinal) && string.Equals(fields.Trigger, "step.completed", StringComparison.Ordinal))
            {
                lines.Add(Labels.ViaAgent(fields.Tokens));
            }
        }

        return lines;
    }
}
