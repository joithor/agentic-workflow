using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): an agent running a Step, and Attach to it.</summary>
internal static partial class Labels
{
    /// <summary>Shown while Attach waits on the item's launch block.</summary>
    internal const string AttachChecking = "Checking for a live session";

    /// <summary>Why Attach is not ready when the item serves no live session and no blocker.</summary>
    internal const string AttachNoLiveSession = "No live session to attach to";

    /// <summary>Why Attach is not ready while a launch is still starting.</summary>
    internal const string AttachLaunchStarting = "A launch is still starting";

    /// <summary>Why Attach is not ready when the item could not be read.</summary>
    internal const string AttachItemUnreadable = "Can't check for a live session now";

    /// <summary>Why Attach is not ready while its send is in flight.</summary>
    internal const string AttachSending = "Sending…";

    /// <summary>Why Attach is not ready after a send, until the item is read again.</summary>
    internal const string AttachSent = "Attach sent; checking the session";

    /// <summary>The launch could not be read.</summary>
    internal const string LaunchUnreadable = "The launch can't be read now.";

    /// <summary>The launch was asked for and has not started.</summary>
    internal const string LaunchRequested = "The session is starting.";

    /// <summary>The launch started, with no viewer to report.</summary>
    internal const string LaunchRunning = "The session is running.";

    /// <summary>The print viewer opens nothing; the attach command is shown to copy.</summary>
    internal const string AttachRunThis = "No viewer is set up. Run this to attach:";

    /// <summary>The run line's lead.</summary>
    /// <param name="launch">The launch id (opaque).</param>
    /// <returns>The line.</returns>
    internal static string RunLine(string launch) => $"Agent · {launch}";

    /// <summary>Accessible name of the run line.</summary>
    /// <param name="launch">The launch id (opaque).</param>
    /// <returns>The name.</returns>
    internal static string RunName(string launch) => $"Agent run {launch}";

    /// <summary>Accessible name of the attach command's Copy.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string CopyAttachName(string address) => $"Copy the attach command for {address}";

    /// <summary>A launch that started with a viewer that opened.</summary>
    /// <param name="viewer">The viewer's label.</param>
    /// <returns>The sentence.</returns>
    internal static string LaunchOpenedIn(string viewer) => $"The session is running, opened in {viewer}.";

    /// <summary>A launch that started with a viewer that did not confirm.</summary>
    /// <param name="viewer">The viewer's label.</param>
    /// <returns>The sentence.</returns>
    internal static string LaunchUnconfirmed(string viewer) => $"The session is running. {viewer} did not confirm that it opened.";

    /// <summary>A launch that started whose viewer failed.</summary>
    /// <param name="code">The viewer's failure code.</param>
    /// <param name="detail">The viewer's sentence.</param>
    /// <returns>The sentence.</returns>
    internal static string LaunchViewerFailed(string code, string detail) =>
        detail.Length == 0 ? $"The session is running. The viewer failed ({code})." : $"The session is running. The viewer failed ({code}): {detail}";

    /// <summary>A launch that failed.</summary>
    /// <param name="code">The failure code.</param>
    /// <param name="detail">The sentence with its fix.</param>
    /// <returns>The sentence.</returns>
    internal static string LaunchFailed(string code, string detail) =>
        detail.Length == 0 ? $"The launch failed ({code})." : $"The launch failed ({code}): {detail}";

    /// <summary>The words for a refusal of an Attach, by its code; null for a code the app has no words for.</summary>
    /// <param name="code">The SND code.</param>
    /// <returns>The sentence, or null.</returns>
    internal static string? AttachRefusal(string code) => code switch
    {
        "SND-LAUNCH-006" => "There is no live session to attach to.",
        "SND-LAUNCH-008" => "A launch is still starting. Try again in a moment.",
        "SND-LAUNCH-001" => "The viewer failed to open.",
        "SND-LAUNCH-009" => "The viewer timed out.",
        "SND-LAUNCH-002" => "No viewer is set up.",
        "SND-ITEM-409" => "The item has changed or is closed. Reload it and try again.",
        "SND-AUTH-002" => "Only you can attach, from the app or a terminal. A session can't.",
        _ => null,
    };

    /// <summary>A completed agent Step.</summary>
    /// <param name="tokens">The tokens spent, when served.</param>
    /// <returns>The words.</returns>
    internal static string ViaAgent(int? tokens) =>
        tokens is { } spent ? string.Create(CultureInfo.InvariantCulture, $"via agent · {spent:N0} tokens") : "via agent";

    /// <summary>A failed agent Step.</summary>
    /// <param name="reason">The served reason (launch, lost, exit, limit).</param>
    /// <param name="to">The state the instance moved to, when served.</param>
    /// <returns>The sentence.</returns>
    internal static string AgentFailed(string? reason, string? to)
    {
        var why = reason switch
        {
            "launch" => "The agent's launch failed",
            "lost" => "The session was lost",
            "exit" => "The agent exited",
            "limit" => "The agent hit its limit",
            _ => "The agent's Step failed",
        };
        return string.IsNullOrEmpty(to) ? why + "." : $"{why}. The instance moved to {to}.";
    }

    /// <summary>The visible words of the link from an agent Step to its session.</summary>
    internal const string SessionLink = "Session ▸";

    /// <summary>The session link's accessible name.</summary>
    /// <param name="sessionRef">The session's ref.</param>
    /// <returns>The words.</returns>
    internal static string SessionLinkName(string sessionRef) => $"{SessionLink} {sessionRef}, opens it on the Agents page";
}
