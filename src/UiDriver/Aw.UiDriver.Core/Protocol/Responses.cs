namespace Aw.UiDriver.Core.Protocol;

/// <summary>The tree below an element.</summary>
/// <param name="Root">The element and its children to the depth asked for.</param>
internal sealed record TreeResponse(NodeInfo Root);

/// <summary>The elements a search matched, in tree order, without their children.</summary>
/// <param name="Matches">The matches (at most the limit).</param>
/// <param name="Total">How many elements matched in all.</param>
internal sealed record SearchResponse(IReadOnlyList<NodeInfo> Matches, int Total);

/// <summary>One element's properties.</summary>
/// <param name="Node">The element.</param>
/// <param name="Properties">Its registered properties by name, as text (a property that can't be read is left out).</param>
internal sealed record PropsResponse(NodeInfo Node, IReadOnlyDictionary<string, string> Properties);

/// <summary>A verb that acted on an element and has nothing more to say.</summary>
/// <param name="Node">The element it acted on.</param>
internal sealed record ActedResponse(NodeInfo Node);

/// <summary>A wait that held.</summary>
/// <param name="Node">The element, or null when the wait was for it to be gone.</param>
/// <param name="WaitedMs">How long it took.</param>
internal sealed record WaitedResponse(NodeInfo? Node, int WaitedMs);

/// <summary>The window's size after the request.</summary>
/// <param name="Width">Width, in device-independent pixels.</param>
/// <param name="Height">Height, in device-independent pixels.</param>
/// <param name="Title">The window's title.</param>
internal sealed record WindowResponse(int Width, int Height, string Title);

/// <summary>Why a call failed; the body of every non-200 answer.</summary>
/// <param name="Code">The AW-UIDRIVER code.</param>
/// <param name="Message">What went wrong, one line.</param>
internal sealed record DriverFailure(string Code, string Message);

/// <summary>The driver.json file: where a running driver answers.</summary>
/// <param name="Schema">Always <see cref="SchemaName"/>.</param>
/// <param name="Url">The origin ('http://127.0.0.1:PORT').</param>
/// <param name="Pid">The app's process id.</param>
internal sealed record DriverEndpoint(string Schema, string Url, int Pid)
{
    /// <summary>The schema this build writes and reads.</summary>
    internal const string SchemaName = "aw-ui-driver/1";

    /// <summary>The endpoint file's name inside the driver directory.</summary>
    internal const string EndpointFile = "driver.json";

    /// <summary>The token file's name inside the driver directory (0600).</summary>
    internal const string TokenFile = "driver.token";

    /// <summary>The header that carries the token.</summary>
    internal const string TokenHeader = "X-Ui-Driver-Token";
}
