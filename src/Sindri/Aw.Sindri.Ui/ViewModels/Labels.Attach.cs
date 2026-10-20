namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the Attach component (screens spec §4.5, rev 8).</summary>
internal static partial class Labels
{
    /// <summary>The main action on a live agent Step.</summary>
    internal const string Attach = "Watch the agent";

    /// <summary>The main action on a tool or model Step, which has no terminal to join.</summary>
    internal const string OpenInstance = "Open run";

    /// <summary>The note under every Attach button (the fixture's sentence).</summary>
    internal const string AttachNote = "Watch the agent's terminal live. Typing takes it over.";

    /// <summary>The chip that stands in for Attach until the command catalog says it is ready (API P10a).</summary>
    internal const string AvailableWithP10a = "Not available in the app yet";

    /// <summary>The chevron menu's Take over.</summary>
    internal const string TakeOver = "Take over";

    /// <summary>The chevron menu's File a request.</summary>
    internal const string FileRequest = "File a request";

    /// <summary>The chevron menu's Cancel (it asks first).</summary>
    internal const string CancelStep = "Cancel";

    /// <summary>The confirm line's button that goes ahead with the cancel.</summary>
    internal const string ConfirmCancel = "Cancel it";

    /// <summary>The confirm line's button that leaves the Step as it is.</summary>
    internal const string KeepStep = "Keep";

    /// <summary>Accessible name of the main button of a live agent Step.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string AttachTo(string address) => $"Watch the agent for {address}";

    /// <summary>Accessible name of the pending chip that stands for Attach.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <param name="reason">Why it waits, as drawn in the chip; it reads on after the colon, so its first letter is lowered.</param>
    /// <returns>The name.</returns>
    internal static string AttachPendingName(string address, string reason) =>
        reason.Length == 0 ? $"Watch the agent for {address}: " : $"Watch the agent for {address}: {char.ToLowerInvariant(reason[0])}{reason[1..]}";

    /// <summary>Accessible name of the main button of a tool or model Step.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string OpenInstanceName(string address) => $"Open run {address}";

    /// <summary>Accessible name of a Workflows row's title button: the visible title first (WCAG 2.5.3, Label in Name), then what it does.</summary>
    /// <param name="subject">The title as drawn.</param>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string OpenInstanceName(string subject, string address) => $"{subject}, open instance {address}";

    /// <summary>Accessible name of the chevron.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string MoreActionsFor(string address) => $"More actions for {address}";

    /// <summary>Accessible name of the chevron menu's Take over.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string TakeOverName(string address) => $"Take over {address}";

    /// <summary>Accessible name of the chevron menu's File a request.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string FileRequestFor(string address) => $"File a request for {address}";

    /// <summary>Accessible name of the chevron menu's Cancel.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string CancelName(string address) => $"Cancel {address}";

    /// <summary>The one-line confirm until the server serves its own (U8-API-9).</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The line.</returns>
    internal static string CancelAsk(string address) => $"Cancel {address}?";

    /// <summary>Accessible name of the confirm line's go-ahead button.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string ConfirmCancelName(string address) => $"{ConfirmCancel}: {address}";

    /// <summary>Accessible name of the confirm line's Keep button.</summary>
    /// <param name="address">The instance address (opaque).</param>
    /// <returns>The name.</returns>
    internal static string KeepName(string address) => $"Keep {address}";
}
