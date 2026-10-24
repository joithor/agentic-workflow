namespace Aw.Sindri.Ui.ViewModels;

/// <summary>The app's own copy (<see cref="Labels"/>, continued): the whole map's node side panel.</summary>
internal static partial class Labels
{
    /// <summary>The panel's accessible name.</summary>
    internal const string ContextNodePanelName = "Item details";

    /// <summary>The close button's words.</summary>
    internal const string ContextNodePanelClose = "Close (Esc)";

    /// <summary>The line while the node is read.</summary>
    internal const string ContextNodePanelLoading = "Reading this item…";

    /// <summary>The line when the read failed.</summary>
    internal const string ContextNodePanelFailed = "Couldn't read this item";

    /// <summary>The heading over the links.</summary>
    internal const string ContextNodePanelLinks = "Linked to";

    /// <summary>Opens the source's link.</summary>
    internal const string ContextNodePanelOpenSource = "Open source ↗";

    /// <summary>One link row: the other node, the way it links and which way.</summary>
    /// <param name="label">The other node's label (or key).</param>
    /// <param name="type">The edge type.</param>
    /// <param name="outgoing">True when the edge leaves the node.</param>
    /// <returns>"→ Tab manager · touches".</returns>
    internal static string ContextNodePanelEdge(string label, string type, bool outgoing) => (outgoing ? "→ " : "← ") + label + " · " + type;

    /// <summary>The source line: its kind and title or reference.</summary>
    /// <param name="kind">The source kind.</param>
    /// <param name="text">The title, else the reference.</param>
    /// <returns>"ticket: LIN-12".</returns>
    internal static string ContextNodePanelSource(string kind, string text) => kind + ": " + text;
}
